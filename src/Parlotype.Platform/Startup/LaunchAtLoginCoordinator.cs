using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.Startup;

/// <summary>
/// The one place that decides whether Parlotype should be registered to start
/// at sign-in, and keeps the OS in step with that decision (ADR-059).
/// </summary>
/// <remarks>
/// <para>
/// Two callers share this: <c>App</c> reconciles once at startup, and the
/// Settings toggle writes through it. Both need the same default-on rule and
/// the same "the OS may disagree" handling, so neither owns it.
/// </para>
/// <para>
/// Nothing here throws. Launch-at-sign-in is a convenience; a registry that
/// will not cooperate must not stop the app from starting or wedge the
/// Settings page.
/// </para>
/// </remarks>
public sealed class LaunchAtLoginCoordinator
{
    private readonly ISettingsService _settings;
    private readonly ILaunchAtLoginService _launchAtLogin;
    private readonly ILogger<LaunchAtLoginCoordinator> _logger;

    public LaunchAtLoginCoordinator(
        ISettingsService settings,
        ILaunchAtLoginService launchAtLogin,
        ILogger<LaunchAtLoginCoordinator> logger)
    {
        _settings = settings;
        _launchAtLogin = launchAtLogin;
        _logger = logger;
    }

    /// <summary>Whether this build can register itself at all.</summary>
    public bool IsSupported => _launchAtLogin.IsSupported;

    /// <summary>
    /// Reads the stored preference, defaulting to <c>true</c> — absent or
    /// unparsable means on (ADR-059), the same convention
    /// <see cref="SettingsKeys.UpdatesCheckAutomatically"/> uses. Existing
    /// installs therefore adopt launch-at-sign-in on their next update; the
    /// first-run tour discloses it and the toggle undoes it in one click.
    /// </summary>
    public async Task<bool> ReadPreferenceAsync(CancellationToken cancellationToken = default)
    {
        var stored = await _settings.GetAsync<string>(SettingsKeys.LaunchAtLogin, cancellationToken);
        return !bool.TryParse(stored, out var enabled) || enabled;
    }

    /// <summary>
    /// Brings the OS registration in line with the stored preference. Safe to
    /// call on every launch: it writes only when the two actually differ.
    /// </summary>
    /// <remarks>
    /// This is also what repairs a registration left pointing at an old install
    /// location — <see cref="ILaunchAtLoginService.GetState"/> reports a stale
    /// path as <see cref="LaunchAtLoginState.Disabled"/>, so the rewrite happens
    /// here without any special case.
    /// </remarks>
    public async Task<LaunchAtLoginState> ReconcileAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            if (!_launchAtLogin.IsSupported)
                return LaunchAtLoginState.Unsupported;

            var wanted = await ReadPreferenceAsync(cancellationToken);
            var actual = _launchAtLogin.GetState();

            // The user's veto in Task Manager outranks our default. Rewriting the
            // entry would not clear it anyway, and flipping the stored preference
            // would erase a decision they made deliberately.
            if (actual == LaunchAtLoginState.BlockedByOperatingSystem)
            {
                _logger.LogInformation(
                    "Launch at sign-in is registered but switched off in Windows — leaving it alone");
                return actual;
            }

            var isEnabled = actual == LaunchAtLoginState.Enabled;
            if (isEnabled == wanted)
                return actual;

            _launchAtLogin.SetEnabled(wanted);
            return _launchAtLogin.GetState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not reconcile launch at sign-in");
            return LaunchAtLoginState.Disabled;
        }
    }

    /// <summary>
    /// Applies a user decision from Settings: persists the preference, then
    /// registers or unregisters. The preference is stored even when the OS
    /// write fails, so the intent survives to the next launch.
    /// </summary>
    public async Task<LaunchAtLoginState> SetEnabledAsync(
        bool enabled,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _settings.SetAsync(SettingsKeys.LaunchAtLogin, enabled.ToString(), cancellationToken);
            _launchAtLogin.SetEnabled(enabled);
            return _launchAtLogin.GetState();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not {Action} launch at sign-in", enabled ? "enable" : "disable");
            return _launchAtLogin.GetState();
        }
    }
}
