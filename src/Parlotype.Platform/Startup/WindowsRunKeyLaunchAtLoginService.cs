using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Parlotype.Core.Settings;
using Velopack.Locators;

namespace Parlotype.Platform.Startup;

/// <summary>
/// <see cref="ILaunchAtLoginService"/> for Windows, backed by the per-user
/// <c>Run</c> key (ADR-059).
/// </summary>
/// <remarks>
/// <para>
/// <c>HKCU\Software\Microsoft\Windows\CurrentVersion\Run</c> needs no elevation,
/// matches Velopack's per-user install (ADR-053), and — unlike a Startup-folder
/// shortcut — is listed in Task Manager → Startup apps, where users go looking
/// when they want something to stop launching.
/// </para>
/// <para>
/// The registered command is the Velopack <em>stub</em> at the install root
/// (<c>%LOCALAPPDATA%\Parlotype\Parlotype.exe</c>), never the versioned binary
/// under <c>current\</c>. Velopack replaces <c>current\</c> wholesale on every
/// update; the stub is the launcher that survives it.
/// </para>
/// </remarks>
[SupportedOSPlatform("windows")]
public sealed class WindowsRunKeyLaunchAtLoginService : ILaunchAtLoginService
{
    /// <summary>Per-user autorun key. HKCU, so no elevation is ever needed.</summary>
    public const string DefaultRunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";

    /// <summary>
    /// Where Explorer records the user's own enable/disable decision from Task
    /// Manager → Startup apps. Separate from the entry itself: disabling there
    /// leaves our <c>Run</c> value in place and simply stops honouring it.
    /// </summary>
    public const string DefaultStartupApprovedKeyPath =
        @"Software\Microsoft\Windows\CurrentVersion\Explorer\StartupApproved\Run";

    /// <summary>
    /// Value name under both keys. Matches the Velopack pack id, which ADR-053
    /// fixes permanently, so the entry keeps its identity across updates.
    /// </summary>
    public const string ValueName = "Parlotype";

    private readonly ILogger<WindowsRunKeyLaunchAtLoginService> _logger;
    private readonly Func<string?> _resolveLaunchTarget;
    private readonly string _runKeyPath;
    private readonly string _startupApprovedKeyPath;

    public WindowsRunKeyLaunchAtLoginService(ILogger<WindowsRunKeyLaunchAtLoginService> logger)
        : this(logger, ResolveInstalledStubPath, DefaultRunKeyPath, DefaultStartupApprovedKeyPath)
    {
    }

    /// <summary>
    /// Test seam: lets a test point the service at a scratch HKCU key and a
    /// fake executable, so the registry code itself is exercised without
    /// touching the real autorun key.
    /// </summary>
    internal WindowsRunKeyLaunchAtLoginService(
        ILogger<WindowsRunKeyLaunchAtLoginService> logger,
        Func<string?> resolveLaunchTarget,
        string runKeyPath,
        string startupApprovedKeyPath)
    {
        _logger = logger;
        _resolveLaunchTarget = resolveLaunchTarget;
        _runKeyPath = runKeyPath;
        _startupApprovedKeyPath = startupApprovedKeyPath;
    }

    public bool IsSupported => _resolveLaunchTarget() is not null;

    public LaunchAtLoginState GetState()
    {
        var expected = _resolveLaunchTarget();
        if (expected is null)
            return LaunchAtLoginState.Unsupported;

        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_runKeyPath);
            var registered = key?.GetValue(ValueName) as string;

            if (string.IsNullOrWhiteSpace(registered))
                return LaunchAtLoginState.Disabled;

            // A veto in Task Manager outranks everything below: the entry is
            // present and simply will not run. Reported before the path check so
            // a stale path never masks it as "needs rewriting".
            if (IsBlockedByOperatingSystem())
                return LaunchAtLoginState.BlockedByOperatingSystem;

            // A registered command pointing somewhere else is not a working
            // registration — it is what a moved or reinstalled app leaves behind.
            // Reporting Disabled makes the startup reconciler rewrite it.
            if (!string.Equals(registered, Quote(expected), StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInformation(
                    "Launch at sign-in points at {Registered}, expected {Expected} — treating as unregistered",
                    registered, Quote(expected));
                return LaunchAtLoginState.Disabled;
            }

            return LaunchAtLoginState.Enabled;
        }
        catch (Exception ex)
        {
            // Locked-down or corrupt registry. Reporting Disabled is the honest
            // answer: we cannot confirm anything will launch.
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
            using var key = Registry.CurrentUser.CreateSubKey(_runKeyPath, writable: true);
            if (key is null)
            {
                _logger.LogWarning("Could not open {Key} for writing", _runKeyPath);
                return false;
            }

            if (enabled)
            {
                key.SetValue(ValueName, Quote(target), RegistryValueKind.String);

                // Writing the entry does not clear an Explorer veto, and this app
                // deliberately does not forge one: StartupApproved is undocumented
                // and Explorer caches it. Report the failure so the UI can send the
                // user to the one place that can undo it.
                if (IsBlockedByOperatingSystem())
                {
                    _logger.LogInformation(
                        "Registered launch at sign-in, but Windows has it disabled in Task Manager → Startup apps");
                    return false;
                }

                _logger.LogInformation("Parlotype will start at sign-in ({Target})", target);
                return true;
            }

            key.DeleteValue(ValueName, throwOnMissingValue: false);
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
    /// Whether Explorer has the entry switched off. The value is a 12-byte blob
    /// whose low bit of byte 0 is the disabled flag (<c>02…</c>/<c>06…</c>
    /// enabled, <c>03…</c> disabled). Absent means never touched, i.e. enabled.
    /// </summary>
    private bool IsBlockedByOperatingSystem()
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(_startupApprovedKeyPath);
            return key?.GetValue(ValueName) is byte[] { Length: > 0 } blob && (blob[0] & 1) == 1;
        }
        catch (Exception ex)
        {
            // Unreadable veto state answers "not blocked" so the toggle keeps
            // working; the worst case is the UI omitting an explanation.
            _logger.LogDebug(ex, "Could not read the Windows startup-approval state");
            return false;
        }
    }

    /// <summary>
    /// The Velopack stub at the install root, or null when this build cannot be
    /// registered — a portable/unpacked copy or anything run straight from the
    /// IDE, whose path is temporary and would leave a broken autorun entry
    /// behind (same reasoning as <c>UpdateState.NotInstalled</c> in ADR-053).
    /// </summary>
    private static string? ResolveInstalledStubPath()
    {
        try
        {
            var locator = VelopackLocator.Current;
            if (locator.CurrentlyInstalledVersion is null || locator.IsPortable)
                return null;

            var root = locator.RootAppDir;
            var exeName = Path.GetFileName(Environment.ProcessPath);
            if (string.IsNullOrEmpty(root) || string.IsNullOrEmpty(exeName))
                return null;

            var stub = Path.Combine(root, exeName);
            return File.Exists(stub) ? stub : null;
        }
        catch
        {
            // VelopackLocator.Current throws when VelopackApp.Run() never ran
            // (headless tests, benchmark CLI). Not installed, as far as we care.
            return null;
        }
    }

    /// <summary>
    /// Run values are parsed as command lines, and <c>%LOCALAPPDATA%</c> sits
    /// under a user profile name that may contain spaces.
    /// </summary>
    private static string Quote(string path) => $"\"{path}\"";
}
