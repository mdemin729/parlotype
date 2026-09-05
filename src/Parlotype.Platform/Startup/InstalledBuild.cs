using Velopack.Locators;

namespace Parlotype.Platform.Startup;

/// <summary>
/// Whether this process is a Velopack-installed build, as opposed to a
/// <c>dotnet run</c> / IDE launch or an unpacked portable copy.
/// </summary>
/// <remarks>
/// Same distinction <c>WindowsRunKeyLaunchAtLoginService</c> draws (ADR-059): a
/// non-installed build runs from a path that is temporary or about to move, so
/// behaviours that would outlive this process (an autorun entry, a lifetime bound
/// to the launcher) must not apply to it.
/// </remarks>
public static class InstalledBuild
{
    /// <summary>
    /// <see langword="true"/> only when Velopack reports an installed,
    /// non-portable version. Any failure — including <c>VelopackApp.Run()</c>
    /// never having run, as in tests and the benchmark CLI — answers
    /// <see langword="false"/>.
    /// </summary>
    public static bool IsInstalled
    {
        get
        {
            try
            {
                var locator = VelopackLocator.Current;
                return locator.CurrentlyInstalledVersion is not null && !locator.IsPortable;
            }
            catch
            {
                return false;
            }
        }
    }
}
