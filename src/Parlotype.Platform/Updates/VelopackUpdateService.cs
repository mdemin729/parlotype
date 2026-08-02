using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Updates;
using Velopack;
using Velopack.Exceptions;
using Velopack.Sources;

namespace Parlotype.Platform.Updates;

/// <summary>
/// <see cref="IUpdateService"/> backed by Velopack's <see cref="UpdateManager"/>,
/// reading the public GitHub release feed (ADR-053).
/// </summary>
/// <remarks>
/// <para>
/// The only endpoint contacted is <see cref="FeedUrl"/>'s releases API —
/// <c>https://api.github.com/repos/mdemin729/parlotype/releases</c> — plus whatever
/// asset URL GitHub hands back for a download. The request is unauthenticated
/// (<c>accessToken: null</c>) and carries no identifying information; nothing about
/// the machine, the install, or usage is transmitted. That property is the reason
/// this class talks to <see cref="GithubSource"/> directly rather than to any
/// telemetry-capable update service.
/// </para>
/// </remarks>
public sealed class VelopackUpdateService : IUpdateService, IDisposable
{
    /// <summary>The public repository whose Releases feed is polled.</summary>
    public const string FeedUrl = "https://github.com/mdemin729/parlotype";

    /// <summary>
    /// Delay before the first automatic check. Long enough to stay clear of
    /// startup — model prewarm, hotkey registration, first window paint.
    /// </summary>
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Gap between automatic checks. Deliberately coarse: this is a desktop
    /// dictation tool, not a browser, and every tick is an outbound request.
    /// </summary>
    private static readonly TimeSpan CheckInterval = TimeSpan.FromHours(6);

    private readonly ISettingsService _settings;
    private readonly ILogger<VelopackUpdateService> _logger;
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _checkLock = new(1, 1);
    private readonly Lazy<UpdateManager> _manager;

    private UpdateStatus _status = new(UpdateState.Idle);
    private UpdateInfo? _pendingDownload;
    private bool _disposed;

    public VelopackUpdateService(ISettingsService settings, ILogger<VelopackUpdateService> logger)
    {
        _settings = settings;
        _logger = logger;
        _manager = new Lazy<UpdateManager>(() => new UpdateManager(
            new GithubSource(FeedUrl, accessToken: null, prerelease: false)));
    }

    public UpdateStatus Status => Volatile.Read(ref _status);

    public event EventHandler<UpdateStatus>? StatusChanged;

    public string? CurrentVersion
    {
        get
        {
            try
            {
                return _manager.Value.CurrentVersion?.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Could not resolve the installed version");
                return null;
            }
        }
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInstalled())
        {
            // Dev builds, `dotnet run`, and the portable zip all land here. This is
            // the normal case during development, so it is a quiet no-op rather
            // than an error: no exception, no dialog, no retry loop.
            _logger.LogInformation(
                "Updates unavailable — this build was not installed by Velopack (dev, IDE, or portable)");
            SetStatus(new UpdateStatus(UpdateState.NotInstalled));
            return Task.CompletedTask;
        }

        // A previous session may have staged an update that was never restarted into.
        if (TryGetStagedUpdate() is { } staged)
            SetStatus(new UpdateStatus(UpdateState.ReadyToApply, staged.Version?.ToString()));

        _ = Task.Run(() => RunPeriodicChecksAsync(_shutdown.Token), CancellationToken.None);
        return Task.CompletedTask;
    }

    public async Task<UpdateStatus> CheckAsync(
        bool userInitiated,
        CancellationToken cancellationToken = default)
    {
        if (!IsInstalled())
            return SetStatus(new UpdateStatus(UpdateState.NotInstalled));

        if (!userInitiated && !await AutomaticChecksEnabledAsync(cancellationToken))
        {
            _logger.LogDebug("Automatic update check skipped — disabled by the user");
            return Status;
        }

        // Serialise: a "Check now" press landing on top of the periodic check
        // should not open a second connection or fight over _pendingDownload.
        await _checkLock.WaitAsync(cancellationToken);
        try
        {
            return await CheckCoreAsync(userInitiated, cancellationToken);
        }
        finally
        {
            _checkLock.Release();
        }
    }

    private async Task<UpdateStatus> CheckCoreAsync(bool userInitiated, CancellationToken cancellationToken)
    {
        var lastChecked = await LoadLastCheckedAsync(cancellationToken);
        SetStatus(new UpdateStatus(UpdateState.Checking, LastCheckedUtc: lastChecked));

        try
        {
            var update = await _manager.Value.CheckForUpdatesAsync();

            var checkedAt = DateTimeOffset.UtcNow;
            await SaveLastCheckedAsync(checkedAt, cancellationToken);
            lastChecked = checkedAt;

            if (update is null)
            {
                _logger.LogInformation("Update check: already on the latest release");
                return SetStatus(new UpdateStatus(UpdateState.UpToDate, LastCheckedUtc: lastChecked));
            }

            var version = update.TargetFullRelease.Version.ToString();
            _logger.LogInformation("Update check: v{Version} available", version);
            SetStatus(new UpdateStatus(UpdateState.UpdateAvailable, version, lastChecked));

            await DownloadAsync(update, version, lastChecked, cancellationToken);
            return Status;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (NotInstalledException ex)
        {
            // Belt and braces: IsInstalled() above should already have caught this,
            // but Velopack also throws it when the install is present yet damaged.
            _logger.LogInformation(ex, "Updates unavailable — no Velopack install detected");
            return SetStatus(new UpdateStatus(UpdateState.NotInstalled));
        }
        catch (Exception ex)
        {
            return ReportFailure(ex, userInitiated, lastChecked, "check for updates");
        }
    }

    private async Task DownloadAsync(
        UpdateInfo update,
        string version,
        DateTimeOffset? lastChecked,
        CancellationToken cancellationToken)
    {
        SetStatus(new UpdateStatus(UpdateState.Downloading, version, lastChecked));
        try
        {
            await _manager.Value.DownloadUpdatesAsync(update, progress: null, cancellationToken);
            _pendingDownload = update;

            _logger.LogInformation("Update v{Version} downloaded — applies on next restart", version);
            SetStatus(new UpdateStatus(UpdateState.ReadyToApply, version, lastChecked));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            // Leave the status at UpdateAvailable rather than ReadyToApply: the
            // release is real, only the download failed, and the next check retries.
            _logger.LogWarning(ex, "Failed to download update v{Version}", version);
            SetStatus(new UpdateStatus(
                UpdateState.UpdateAvailable, version, lastChecked, Describe(ex, "download the update")));
        }
    }

    public async Task<bool> ApplyAndRestartAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInstalled())
            return false;

        var asset = _pendingDownload?.TargetFullRelease ?? TryGetStagedUpdate();
        if (asset is null)
            return false;

        _logger.LogInformation("Applying update v{Version} and restarting", asset.Version);

        await Task.CompletedTask;

        // Does not return: the process exits, Update.exe swaps `current`, and the
        // app relaunches.
        _manager.Value.ApplyUpdatesAndRestart(asset);
        return true;
    }

    private async Task RunPeriodicChecksAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(InitialDelay, cancellationToken);

            using var timer = new PeriodicTimer(CheckInterval);
            do
            {
                // Re-read the setting each tick so switching the toggle off takes
                // effect without a restart.
                if (await AutomaticChecksEnabledAsync(cancellationToken))
                    await CheckAsync(userInitiated: false, cancellationToken);
            }
            while (await timer.WaitForNextTickAsync(cancellationToken));
        }
        catch (OperationCanceledException)
        {
            // App shutting down.
        }
        catch (Exception ex)
        {
            // The loop is best-effort; losing it must never take the app with it.
            _logger.LogWarning(ex, "Background update checker stopped");
        }
    }

    /// <summary>Default-on; see <see cref="SettingsKeys.UpdatesCheckAutomatically"/>.</summary>
    private async Task<bool> AutomaticChecksEnabledAsync(CancellationToken cancellationToken)
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.UpdatesCheckAutomatically, cancellationToken);
        return !bool.TryParse(saved, out var enabled) || enabled;
    }

    private async Task<DateTimeOffset?> LoadLastCheckedAsync(CancellationToken cancellationToken)
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.UpdatesLastCheckedUtc, cancellationToken);
        return DateTimeOffset.TryParse(saved, out var parsed) ? parsed : null;
    }

    private Task SaveLastCheckedAsync(DateTimeOffset value, CancellationToken cancellationToken) =>
        _settings.SetAsync(SettingsKeys.UpdatesLastCheckedUtc, value.ToString("O"), cancellationToken);

    private bool IsInstalled()
    {
        try
        {
            return _manager.Value.IsInstalled;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Velopack install probe failed — treating as not installed");
            return false;
        }
    }

    private VelopackAsset? TryGetStagedUpdate()
    {
        try
        {
            return _manager.Value.UpdatePendingRestart;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Could not read the staged update");
            return null;
        }
    }

    private UpdateStatus ReportFailure(
        Exception ex,
        bool userInitiated,
        DateTimeOffset? lastChecked,
        string action)
    {
        if (userInitiated)
            _logger.LogWarning(ex, "Update check failed (user-initiated)");
        else
            // Automatic path: being offline is the overwhelmingly common cause and
            // is not worth a warning, let alone anything the user has to dismiss.
            _logger.LogDebug(ex, "Automatic update check failed");

        return SetStatus(new UpdateStatus(
            UpdateState.Failed, Status.AvailableVersion, lastChecked, Describe(ex, action)));
    }

    private static string Describe(Exception ex, string action) => ex switch
    {
        HttpRequestException or TaskCanceledException or TimeoutException =>
            $"Could not reach the update server. Check your connection and try again.",
        _ => $"Failed to {action}: {ex.Message}",
    };

    private UpdateStatus SetStatus(UpdateStatus status)
    {
        Volatile.Write(ref _status, status);
        StatusChanged?.Invoke(this, status);
        return status;
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        _shutdown.Cancel();
        _shutdown.Dispose();
        _checkLock.Dispose();
    }
}
