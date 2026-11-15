using System.Runtime.Versioning;
using System.Security;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.Startup;

/// <summary>
/// <see cref="ILaunchAtLoginService"/> for macOS, backed by a per-user launch
/// agent (ADR-077). The macOS counterpart of
/// <see cref="WindowsRunKeyLaunchAtLoginService"/>.
/// </summary>
/// <remarks>
/// <para>
/// A property list in <c>~/Library/LaunchAgents</c> with <c>RunAtLoad</c> is
/// loaded by <c>launchd</c> at the next sign-in. It needs no elevation and no
/// helper bundle, and it is listed under System Settings → General → Login
/// Items, where users go looking when they want something to stop launching.
/// Nothing is loaded into the running session: the file only has to exist at
/// the next sign-in, so this class never runs <c>launchctl</c>.
/// </para>
/// <para>
/// Only a copy that lives in an Applications folder registers itself. A build
/// run from the repository, a download folder or a mounted disk image has a
/// path that is temporary or about to move, and registering it would leave an
/// entry behind that points at nothing — the same reasoning that keeps
/// <c>dotnet run</c> builds out of the Windows Run key (ADR-059).
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class MacOsLaunchAgentLaunchAtLoginService : ILaunchAtLoginService
{
    /// <summary>
    /// The launch agent's label and file name. Matches the application bundle
    /// identifier, which is what associates the entry with Parlotype in Login
    /// Items.
    /// </summary>
    public const string Label = "dev.parlotype.Parlotype";

    private const string BundleExecutableMarker = ".app/Contents/MacOS/";

    private readonly ILogger<MacOsLaunchAgentLaunchAtLoginService> _logger;
    private readonly Func<string?> _resolveLaunchTarget;
    private readonly string _agentFilePath;

    public MacOsLaunchAgentLaunchAtLoginService(ILogger<MacOsLaunchAgentLaunchAtLoginService> logger)
        : this(logger, () => ResolveInstalledExecutable(Environment.ProcessPath, HomeDirectory), DefaultAgentsDirectory)
    {
    }

    /// <summary>
    /// Test seam: lets a test point the service at a scratch folder and a fake
    /// executable, so the file handling is exercised without touching the real
    /// launch agents folder.
    /// </summary>
    internal MacOsLaunchAgentLaunchAtLoginService(
        ILogger<MacOsLaunchAgentLaunchAtLoginService> logger,
        Func<string?> resolveLaunchTarget,
        string launchAgentsDirectory)
    {
        _logger = logger;
        _resolveLaunchTarget = resolveLaunchTarget;
        _agentFilePath = Path.Combine(launchAgentsDirectory, Label + ".plist");
    }

    public bool IsSupported => _resolveLaunchTarget() is not null;

    public LaunchAtLoginState GetState()
    {
        var expected = _resolveLaunchTarget();
        if (expected is null)
            return LaunchAtLoginState.Unsupported;

        try
        {
            if (!File.Exists(_agentFilePath))
                return LaunchAtLoginState.Disabled;

            // An agent that starts some other path is not a working
            // registration — it is what a moved or renamed app leaves behind.
            // Reporting Disabled makes the startup reconciler rewrite it.
            if (!File.ReadAllText(_agentFilePath).Contains(ProgramArgument(expected), StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "The launch agent does not start {Expected} — treating as unregistered", expected);
                return LaunchAtLoginState.Disabled;
            }

            return LaunchAtLoginState.Enabled;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not read the launch-at-sign-in registration");
            return LaunchAtLoginState.Disabled;
        }
    }

    public bool SetEnabled(bool enabled)
    {
        var target = _resolveLaunchTarget();
        if (target is null)
        {
            _logger.LogDebug("Launch at sign-in is unavailable for this build — ignoring the request");
            return false;
        }

        try
        {
            if (enabled)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_agentFilePath)!);
                File.WriteAllText(_agentFilePath, BuildPropertyList(target));
                _logger.LogInformation("Parlotype will start at sign-in ({Target})", target);
                return true;
            }

            File.Delete(_agentFilePath);
            _logger.LogInformation("Parlotype will no longer start at sign-in");
            return true;
        }
        catch (Exception ex)
        {
            // Never fatal: failing to register is a lost convenience, not a
            // reason to disrupt startup or the Settings page.
            _logger.LogWarning(ex, "Could not {Action} launch at sign-in", enabled ? "enable" : "disable");
            return false;
        }
    }

    /// <summary>
    /// The executable to register, or null when this copy should not register
    /// itself: it is not inside an application bundle, or the bundle is not in
    /// <c>/Applications</c> or <c>~/Applications</c>.
    /// </summary>
    internal static string? ResolveInstalledExecutable(string? processPath, string homeDirectory)
    {
        if (string.IsNullOrEmpty(processPath))
            return null;

        var marker = processPath.IndexOf(BundleExecutableMarker, StringComparison.Ordinal);
        if (marker < 0)
            return null;

        // "/Applications/Parlotype.app" -> its containing folder.
        var bundlePath = processPath[..(marker + ".app".Length)];
        var container = Path.GetDirectoryName(bundlePath);

        var userApplications = Path.Combine(homeDirectory, "Applications");
        return container is "/Applications" || string.Equals(container, userApplications, StringComparison.Ordinal)
            ? processPath
            : null;
    }

    private static string BuildPropertyList(string executablePath) =>
        $"""
        <?xml version="1.0" encoding="UTF-8"?>
        <!DOCTYPE plist PUBLIC "-//Apple//DTD PLIST 1.0//EN" "http://www.apple.com/DTDs/PropertyList-1.0.dtd">
        <plist version="1.0">
        <dict>
            <key>Label</key>
            <string>{Label}</string>
            <key>AssociatedBundleIdentifiers</key>
            <string>{Label}</string>
            <key>ProgramArguments</key>
            <array>
                {ProgramArgument(executablePath)}
            </array>
            <key>RunAtLoad</key>
            <true/>
            <key>ProcessType</key>
            <string>Interactive</string>
        </dict>
        </plist>

        """;

    private static string ProgramArgument(string executablePath) =>
        $"<string>{SecurityElement.Escape(executablePath)}</string>";

    private static string HomeDirectory =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Where <c>launchd</c> looks for per-user agents. Fixed by the operating
    /// system, like the Run key on Windows, so it is not one of the app-owned
    /// locations <see cref="IAppPaths"/> governs.
    /// </summary>
    private static string DefaultAgentsDirectory =>
        Path.Combine(HomeDirectory, "Library", "LaunchAgents");
}
