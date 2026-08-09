namespace Parlotype.Core.Settings;

/// <summary>
/// What the operating system will actually do at the next sign-in — as opposed
/// to what <see cref="SettingsKeys.LaunchAtLogin"/> says we asked for. The two
/// can disagree, which is the whole reason this is an enum and not a bool.
/// </summary>
public enum LaunchAtLoginState
{
    /// <summary>
    /// This build cannot register itself: a non-Windows platform, or a build
    /// that was not installed by Setup.exe (<c>dotnet run</c>, the IDE, an
    /// unpacked zip). Registering the path of a build that moves or is deleted
    /// leaves a broken entry behind that outlives the app.
    /// </summary>
    Unsupported,

    /// <summary>Nothing is registered; Parlotype will not start at sign-in.</summary>
    Disabled,

    /// <summary>Registered and permitted; Parlotype starts at sign-in.</summary>
    Enabled,

    /// <summary>
    /// Registered by us, but the user switched it off in Task Manager →
    /// Startup apps (or an equivalent tool). Windows records that veto
    /// separately from the entry itself, so the entry still exists and does
    /// nothing. Surfaced as its own state so the UI can explain the situation
    /// instead of showing a switch that reads "on" while nothing launches.
    /// </summary>
    BlockedByOperatingSystem,
}

/// <summary>
/// Registers or unregisters Parlotype to start when the user signs in
/// (ADR-059).
/// </summary>
/// <remarks>
/// <para>
/// Parlotype is a global-hotkey tool that starts tray-only, so being present
/// from sign-in is what makes the hotkey work at all — hence the default-on
/// policy. That policy lives in the caller; implementations of this interface
/// only report and change the OS registration.
/// </para>
/// <para>
/// Every member is synchronous and cheap (a registry read or write on
/// Windows), and every member is safe to call on an unsupported platform,
/// where reads answer <see cref="LaunchAtLoginState.Unsupported"/> and writes
/// do nothing. Implementations must never throw: a failure to register is a
/// degraded convenience, never a reason to interrupt startup.
/// </para>
/// </remarks>
public interface ILaunchAtLoginService
{
    /// <summary>
    /// Whether this build can register itself at all. False for non-Windows
    /// platforms and for builds Velopack did not install; the Settings page
    /// greys the toggle out and says why rather than offering a control that
    /// can only fail.
    /// </summary>
    bool IsSupported { get; }

    /// <summary>Reads what the OS will do at the next sign-in. Never throws.</summary>
    LaunchAtLoginState GetState();

    /// <summary>
    /// Registers (<paramref name="enabled"/> true) or unregisters Parlotype for
    /// launch at sign-in. Idempotent, and a no-op when
    /// <see cref="IsSupported"/> is false.
    /// </summary>
    /// <returns>
    /// True when the OS now matches the request. False when it could not be
    /// applied — unsupported build, a registry error, or a Windows-side veto
    /// this app cannot clear (<see cref="LaunchAtLoginState.BlockedByOperatingSystem"/>).
    /// </returns>
    bool SetEnabled(bool enabled);
}
