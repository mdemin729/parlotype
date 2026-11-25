namespace Parlotype.Core.Settings;

/// <summary>An operating-system privacy grant Parlotype cannot work without.</summary>
public enum SystemPermission
{
    /// <summary>
    /// macOS Accessibility: required to observe the global dictation hotkey and
    /// to post the paste keystroke into another application.
    /// </summary>
    Accessibility,

    /// <summary>Access to the microphone.</summary>
    Microphone,
}

/// <summary>What the operating system currently allows for a <see cref="SystemPermission"/>.</summary>
public enum SystemPermissionState
{
    /// <summary>Allowed, or the platform has no such gate.</summary>
    Granted,

    /// <summary>The user has not been asked yet; requesting will show the system prompt.</summary>
    NotDetermined,

    /// <summary>
    /// Refused, restricted by policy, or (for Accessibility, which macOS reports
    /// only as on or off) simply not switched on yet. Only the user can change
    /// it, in System Settings.
    /// </summary>
    Denied,
}

/// <summary>
/// Reads and requests the privacy grants the operating system gates Parlotype's
/// core functions behind (ADR-078).
/// </summary>
/// <remarks>
/// macOS is the only platform with such gates today. Everywhere else the
/// implementation reports <see cref="SystemPermissionState.Granted"/> for
/// everything, so callers need no platform checks of their own. Implementations
/// never throw.
/// </remarks>
public interface ISystemPermissionService
{
    /// <summary>The current state. Cheap enough to poll.</summary>
    SystemPermissionState GetState(SystemPermission permission);

    /// <summary>
    /// Asks the operating system to prompt the user where it has a prompt to
    /// show, and returns the state afterwards. For the microphone this waits
    /// for the user's answer; for Accessibility macOS only opens a dialog that
    /// points at System Settings, so the returned state is usually still
    /// <see cref="SystemPermissionState.Denied"/> and the caller should poll
    /// <see cref="GetState"/>.
    /// </summary>
    Task<SystemPermissionState> RequestAsync(SystemPermission permission, CancellationToken cancellationToken = default);

    /// <summary>
    /// Opens the system settings page where the user can grant the permission.
    /// Returns false when there is no such page or it could not be opened.
    /// </summary>
    bool OpenSettings(SystemPermission permission);
}
