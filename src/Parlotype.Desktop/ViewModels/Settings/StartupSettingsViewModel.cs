using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Settings;
using Parlotype.Platform.Startup;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Surfaces the launch-at-sign-in toggle (ADR-059) — on by default, one click
/// to turn off.
/// </summary>
/// <remarks>
/// The switch reflects what Windows will actually do, not merely what was
/// stored: a build that cannot register itself, or an entry the user vetoed in
/// Task Manager, both show as off with an explanation instead of a switch that
/// reads "on" while nothing launches.
/// </remarks>
public partial class StartupSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly LaunchAtLoginCoordinator _coordinator;

    /// <summary>
    /// Set while the switch is being driven from stored/observed state rather
    /// than by the user, so the refresh does not echo back as an edit and write
    /// the value straight out again.
    /// </summary>
    private bool _suppressWrite = true;

    public override string Title => "Startup";
    public override SettingsCategory Category => SettingsCategory.Application;

    [ObservableProperty]
    private bool _launchAtLogin = true;

    /// <summary>
    /// False for builds that cannot register themselves — non-Windows, or a
    /// build that did not come from Setup.exe. The view greys the switch out
    /// and says why.
    /// </summary>
    [ObservableProperty]
    private bool _isSupported = true;

    /// <summary>
    /// Set when Windows itself has the entry switched off. Only Task Manager
    /// can undo that, so the view points the user there rather than offering a
    /// control that cannot win.
    /// </summary>
    [ObservableProperty]
    private bool _isBlockedByWindows;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// The last write, exposed so tests can await it rather than poll. Settings
    /// writes are fire-and-forget here — unlike the uninstall consent flag, a
    /// lost autostart preference costs a click, not data.
    /// </summary>
    public Task PendingWrite { get; private set; } = Task.CompletedTask;

    public StartupSettingsViewModel(LaunchAtLoginCoordinator coordinator)
    {
        _coordinator = coordinator;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var stored = await _coordinator.ReadPreferenceAsync();
        // Reconcile rather than merely read: App already did this at startup, so
        // this is normally a no-op read, but it means the page always shows what
        // the OS will actually do — and repairs the registration if the startup
        // pass failed or the entry has gone stale since.
        Apply(stored, await _coordinator.ReconcileAsync());
        _suppressWrite = false;
    }

    partial void OnLaunchAtLoginChanged(bool value)
    {
        if (_suppressWrite)
            return;

        PendingWrite = ApplyUserChoiceAsync(value);
    }

    private async Task ApplyUserChoiceAsync(bool enabled)
    {
        var state = await _coordinator.SetEnabledAsync(enabled);
        Apply(enabled, state);
    }

    /// <summary>
    /// Renders the stored preference against what the OS actually reports.
    /// Where the two disagree, the OS wins and <see cref="StatusText"/>
    /// explains it.
    /// </summary>
    private void Apply(bool preference, LaunchAtLoginState state)
    {
        IsSupported = _coordinator.IsSupported;
        IsBlockedByWindows = state == LaunchAtLoginState.BlockedByOperatingSystem;

        var effective = state switch
        {
            LaunchAtLoginState.Enabled => true,
            LaunchAtLoginState.Disabled or LaunchAtLoginState.Unsupported => false,
            // Blocked: the preference is still "on", and the switch should keep
            // showing that. The warning line carries the bad news.
            _ => preference,
        };

        // A refresh, not an edit — suppressed so it does not loop back through
        // OnLaunchAtLoginChanged and write what we just read.
        var wasSuppressed = _suppressWrite;
        _suppressWrite = true;
        try
        {
            LaunchAtLogin = effective;
        }
        finally
        {
            _suppressWrite = wasSuppressed;
        }

        StatusText = !IsSupported
            ? "This build cannot start itself at sign-in — it was not installed from Setup.exe."
            : IsBlockedByWindows
                ? "Windows has Parlotype switched off in Task Manager → Startup apps. Re-enable it there to let this setting take effect."
                : effective
                    ? "Parlotype starts with Windows and waits in the tray — no window opens."
                    : "Parlotype only runs when you start it yourself.";
    }
}
