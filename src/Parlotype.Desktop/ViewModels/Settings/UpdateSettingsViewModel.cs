using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Updates;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Surfaces the updater: the automatic-check opt-out, when the feed was last
/// reached, and a manual "Check now" (ADR-053).
/// </summary>
/// <remarks>
/// The update check is the only outbound request Parlotype makes in local mode,
/// so this page states plainly what is contacted and lets the user turn it off.
/// </remarks>
public partial class UpdateSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IUpdateService _updates;
    private readonly ILogger<UpdateSettingsViewModel> _logger;

    /// <summary>Guards against the initial settings load echoing back as a user edit.</summary>
    private bool _loading = true;

    public override string Title => "Updates";
    public override SettingsCategory Category => SettingsCategory.Application;

    [ObservableProperty]
    private bool _checkAutomatically = true;

    [ObservableProperty]
    private string _statusText = "Not checked yet.";

    [ObservableProperty]
    private string _lastCheckedText = "Never";

    [ObservableProperty]
    private string _currentVersionText = "development build";

    /// <summary>True once an update is downloaded and only a restart is missing.</summary>
    [ObservableProperty]
    private bool _isRestartRequired;

    /// <summary>
    /// False for builds that cannot update themselves (IDE, <c>dotnet run</c>,
    /// portable zip). The view greys the controls out and says why, rather than
    /// offering a button that can only fail.
    /// </summary>
    [ObservableProperty]
    private bool _isUpdateSupported = true;

    [ObservableProperty]
    private bool _isBusy;

    public UpdateSettingsViewModel(
        ISettingsService settings,
        IUpdateService updates,
        ILogger<UpdateSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _updates = updates;
        _logger = logger ?? NullLogger<UpdateSettingsViewModel>.Instance;

        _updates.StatusChanged += OnStatusChanged;
        Apply(_updates.Status);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.UpdatesCheckAutomatically);
        // Default-on: absent or unparseable means enabled (ADR-053).
        CheckAutomatically = !bool.TryParse(saved, out var enabled) || enabled;
        _loading = false;

        CurrentVersionText = _updates.CurrentVersion ?? "development build";
    }

    partial void OnCheckAutomaticallyChanged(bool value)
    {
        if (_loading)
            return;

        _logger.LogInformation("Automatic update checks: {Enabled}", value);
        _ = _settings.SetAsync(SettingsKeys.UpdatesCheckAutomatically, value.ToString());
    }

    [RelayCommand]
    private async Task CheckNowAsync()
    {
        IsBusy = true;
        try
        {
            // userInitiated: runs even with automatic checks off, and reports
            // failures instead of swallowing them.
            await _updates.CheckAsync(userInitiated: true);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task RestartNowAsync()
    {
        // Does not return when it succeeds — the process exits and relaunches.
        if (!await _updates.ApplyAndRestartAsync())
            _logger.LogInformation("Restart requested but no update is staged");
    }

    private void OnStatusChanged(object? sender, UpdateStatus status)
    {
        // IUpdateService raises this from its background checker.
        if (Dispatcher.UIThread.CheckAccess())
            Apply(status);
        else
            Dispatcher.UIThread.Post(() => Apply(status));
    }

    private void Apply(UpdateStatus status)
    {
        IsUpdateSupported = status.State != UpdateState.NotInstalled;
        IsRestartRequired = status.State == UpdateState.ReadyToApply;

        StatusText = status.State switch
        {
            UpdateState.Idle => "Not checked yet.",
            UpdateState.NotInstalled =>
                "This build cannot update itself — it was not installed from Setup.exe.",
            UpdateState.Checking => "Checking for updates…",
            UpdateState.UpToDate => "Parlotype is up to date.",
            UpdateState.UpdateAvailable => status.Message
                ?? $"Version {status.AvailableVersion} is available.",
            UpdateState.Downloading => $"Downloading version {status.AvailableVersion}…",
            UpdateState.ReadyToApply =>
                $"Version {status.AvailableVersion} is ready. Restart to finish updating.",
            UpdateState.Failed => status.Message ?? "The update check failed.",
            _ => string.Empty,
        };

        LastCheckedText = status.LastCheckedUtc is { } checkedAt
            ? checkedAt.ToLocalTime().ToString("f")
            : "Never";
    }
}
