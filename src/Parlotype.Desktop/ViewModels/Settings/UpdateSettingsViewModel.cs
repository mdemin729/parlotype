using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Updates;
using Parlotype.Desktop.Resources;

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

    public override string Title => Strings.Settings_Updates_Title;
    public override SettingsCategory Category => SettingsCategory.Application;

    [ObservableProperty]
    private bool _checkAutomatically = true;

    [ObservableProperty]
    private string _statusText = Strings.Settings_Updates_Status_NotChecked;

    [ObservableProperty]
    private string _lastCheckedText = Strings.Settings_Updates_Never;

    [ObservableProperty]
    private string _currentVersionText = Strings.Settings_Updates_DevelopmentBuild;

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

        CurrentVersionText = _updates.CurrentVersion ?? Strings.Settings_Updates_DevelopmentBuild;
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
            UpdateState.Idle => Strings.Settings_Updates_Status_NotChecked,
            UpdateState.NotInstalled => Strings.Settings_Updates_Status_NotInstalled,
            UpdateState.Checking => Strings.Settings_Updates_Status_Checking,
            UpdateState.UpToDate => Strings.Settings_Updates_Status_UpToDate,
            UpdateState.UpdateAvailable => status.Message
                ?? Strings.Format_Settings_Updates_Status_AvailableFormat(status.AvailableVersion),
            UpdateState.Downloading =>
                Strings.Format_Settings_Updates_Status_DownloadingFormat(status.AvailableVersion),
            // Staged, not installed. It applies as Parlotype closes; the button
            // just brings that forward. Saying "restart" alone would be a promise
            // an ordinary quit-and-relaunch does not keep (ADR-053).
            UpdateState.ReadyToApply =>
                Strings.Format_Settings_Updates_Status_ReadyFormat(status.AvailableVersion),
            UpdateState.Failed => status.Message ?? Strings.Settings_Updates_Status_Failed,
            _ => string.Empty,
        };

        LastCheckedText = status.LastCheckedUtc is { } checkedAt
            ? checkedAt.ToLocalTime().ToString("f")
            : Strings.Settings_Updates_Never;
    }
}
