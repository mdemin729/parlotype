using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Parlotype.Platform.Startup;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Binds a development build's lifetime to the process that launched it (ADR-062).
/// </summary>
/// <remarks>
/// <para>
/// Parlotype is tray-first with <c>ShutdownMode.OnExplicitShutdown</c>, and the
/// global keyboard hook runs on a background thread, so a <c>dotnet run</c> child
/// that its launcher fails to kill on stop keeps running headless: no window, a
/// tray icon in the overflow, and the dictation hotkey still injecting text. The
/// single-instance guard then makes every later <c>dotnet run</c> defer to that
/// orphan.
/// </para>
/// <para>
/// The fix is dev-only: watch the launching process and, when it exits, run the
/// normal graceful shutdown, with a hard <see cref="Environment.Exit(int)"/>
/// fallback if that stalls. Installed builds — launched by Explorer or a Velopack
/// stub that exits immediately — are excluded entirely.
/// </para>
/// </remarks>
public sealed class ParentProcessExitWatcher : IDisposable
{
    private static readonly TimeSpan DefaultForceExitDelay = TimeSpan.FromSeconds(5);

    private readonly ILogger<ParentProcessExitWatcher> _logger;
    private readonly Func<bool> _isInstalledBuild;
    private readonly Func<ParentProcessRef?> _resolveParent;
    private readonly TimeSpan _forceExitDelay;
    private readonly Action _forceExit;
    private readonly CancellationTokenSource _cts = new();

    private bool _started;
    private bool _disposed;

    public ParentProcessExitWatcher(ILogger<ParentProcessExitWatcher> logger)
        : this(logger, isInstalledBuild: null, resolveParent: null, forceExitDelay: null, forceExit: null)
    {
    }

    /// <summary>Test seam — production defaults when arguments are null.</summary>
    internal ParentProcessExitWatcher(
        ILogger<ParentProcessExitWatcher> logger,
        Func<bool>? isInstalledBuild,
        Func<ParentProcessRef?>? resolveParent,
        TimeSpan? forceExitDelay,
        Action? forceExit)
    {
        _logger = logger;
        _isInstalledBuild = isInstalledBuild ?? (() => InstalledBuild.IsInstalled);
        _resolveParent = resolveParent ?? (() => ResolveParentProcess(_logger));
        _forceExitDelay = forceExitDelay ?? DefaultForceExitDelay;
        _forceExit = forceExit ?? (() => Environment.Exit(0));
    }

    /// <summary>
    /// Arms the watchdog. No-ops for an installed build, on a non-Windows OS, or
    /// when the launching process cannot be resolved to a live one — a detection
    /// miss must never take the app down.
    /// </summary>
    /// <param name="requestShutdown">
    /// Invoked (from a background thread) when the launcher exits; expected to
    /// marshal to the UI thread and call <c>desktop.Shutdown()</c>.
    /// </param>
    public void Start(Action requestShutdown)
    {
        ArgumentNullException.ThrowIfNull(requestShutdown);

        if (_started || _disposed)
            return;

        _started = true;

        if (_isInstalledBuild())
        {
            _logger.LogDebug("Installed build — parent-process watchdog stays off");
            return;
        }

        var parent = _resolveParent();
        if (parent is null)
        {
            _logger.LogInformation(
                "Launching process could not be resolved to a live one — dev watchdog not armed");
            return;
        }

        _logger.LogInformation(
            "Dev watchdog armed — this instance will shut down when {Name} ({Pid}) exits",
            parent.Name, parent.Id);

        _ = WatchAsync(parent, requestShutdown);
    }

    private async Task WatchAsync(ParentProcessRef parent, Action requestShutdown)
    {
        try
        {
            await parent.WaitForExitAsync(_cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // disposed — a clean tray Exit, nothing to do
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex,
                "Waiting on the launching process failed — treating it as gone");
        }

        if (_cts.IsCancellationRequested)
            return;

        _logger.LogWarning(
            "Launching process {Name} ({Pid}) exited — shutting down this development instance",
            parent.Name, parent.Id);

        try
        {
            requestShutdown();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Graceful shutdown request threw — the force-exit fallback still applies");
        }

        try
        {
            await Task.Delay(_forceExitDelay, _cts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return; // graceful shutdown completed and disposed us
        }

        _logger.LogWarning(
            "Graceful shutdown did not finish within {Seconds:n0}s of the launcher exiting — forcing exit",
            _forceExitDelay.TotalSeconds);
        _forceExit();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        try
        {
            _cts.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Already torn down elsewhere.
        }

        _cts.Dispose();
    }

    private static ParentProcessRef? ResolveParentProcess(ILogger logger)
    {
        if (!NativeParentProcess.TryGetParentProcessId(out var pid))
            return null;

        try
        {
            var process = Process.GetProcessById(pid);
            if (process.HasExited)
                return null;

            var name = SafeProcessName(process);
            return new ParentProcessRef(pid, name, process.WaitForExitAsync);
        }
        catch (ArgumentException)
        {
            // No process with that id — the launcher is already gone.
            return null;
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Could not open the launching process ({Pid})", pid);
            return null;
        }
    }

    private static string SafeProcessName(Process process)
    {
        try
        {
            return process.ProcessName;
        }
        catch (Exception)
        {
            return "unknown";
        }
    }
}

/// <summary>A resolved launching process: enough to name it and await its exit.</summary>
internal sealed record ParentProcessRef(int Id, string Name, Func<CancellationToken, Task> WaitForExitAsync);
