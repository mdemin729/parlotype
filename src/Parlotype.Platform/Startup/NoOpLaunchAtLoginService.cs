using Parlotype.Core.Settings;

namespace Parlotype.Platform.Startup;

/// <summary>
/// <see cref="ILaunchAtLoginService"/> for platforms with no implementation yet
/// (macOS, Linux). Reports <see cref="LaunchAtLoginState.Unsupported"/> so the
/// Settings page greys the toggle out with a reason, rather than offering a
/// switch that silently does nothing.
/// </summary>
/// <remarks>
/// macOS would use a <c>SMAppService</c> login item and Linux an XDG autostart
/// <c>.desktop</c> file; both are real work and neither ships today (ADR-059).
/// </remarks>
public sealed class NoOpLaunchAtLoginService : ILaunchAtLoginService
{
    public bool IsSupported => false;

    public LaunchAtLoginState GetState() => LaunchAtLoginState.Unsupported;

    public bool SetEnabled(bool enabled) => false;
}
