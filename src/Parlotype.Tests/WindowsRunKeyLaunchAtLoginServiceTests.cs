using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Win32;
using Parlotype.Core.Settings;
using Parlotype.Platform.Startup;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Exercises the real registry code (ADR-059) against scratch keys under HKCU,
/// never the live <c>Run</c> key. Windows-only; every test no-ops elsewhere,
/// matching <c>WindowsNvidiaEnvironmentProviderTests</c>.
/// </summary>
public class WindowsRunKeyLaunchAtLoginServiceTests : IDisposable
{
    /// <summary>
    /// Scratch root, unique per test class instance so parallel runs cannot
    /// collide. Removed in <see cref="Dispose"/>.
    /// </summary>
    private const string ScratchParent = @"Software\Parlotype.Tests";

    private readonly string _root = $@"{ScratchParent}\{Guid.NewGuid():N}";

    private string RunKeyPath => $@"{_root}\Run";
    private string ApprovedKeyPath => $@"{_root}\StartupApproved";

    private const string FakeExe = @"C:\Parlotype\Parlotype.exe";

    private WindowsRunKeyLaunchAtLoginService Build(string? target = FakeExe)
    {
        // Guarded by the OperatingSystem.IsWindows() early-return in each test;
        // the type is [SupportedOSPlatform("windows")].
        if (!OperatingSystem.IsWindows())
            throw new InvalidOperationException("Windows-only");

        return new WindowsRunKeyLaunchAtLoginService(
            NullLogger<WindowsRunKeyLaunchAtLoginService>.Instance,
            () => target,
            RunKeyPath,
            ApprovedKeyPath);
    }

    [Fact]
    public void GetState_WithNothingRegistered_IsDisabled()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.Equal(LaunchAtLoginState.Disabled, Build().GetState());
    }

    [Fact]
    public void SetEnabled_WritesAQuotedPath_AndReadsBackAsEnabled()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var service = Build();

        Assert.True(service.SetEnabled(true));
        Assert.Equal(LaunchAtLoginState.Enabled, service.GetState());

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        // Quoted because %LOCALAPPDATA% sits under a profile name that may
        // contain spaces, and Run values are parsed as command lines.
        Assert.Equal(
            $"\"{FakeExe}\"",
            key?.GetValue(WindowsRunKeyLaunchAtLoginService.ValueName));
    }

    [Fact]
    public void SetEnabled_False_RemovesTheValue()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var service = Build();
        service.SetEnabled(true);

        Assert.True(service.SetEnabled(false));
        Assert.Equal(LaunchAtLoginState.Disabled, service.GetState());

        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
        Assert.Null(key?.GetValue(WindowsRunKeyLaunchAtLoginService.ValueName));
    }

    [Fact]
    public void SetEnabled_False_WithNothingRegistered_Succeeds()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // DeleteValue(throwOnMissingValue: false) — turning off something that
        // was never on is a no-op, not an error.
        Assert.True(Build().SetEnabled(false));
    }

    [Fact]
    public void SetEnabled_IsIdempotent()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var service = Build();
        service.SetEnabled(true);
        service.SetEnabled(true);

        Assert.Equal(LaunchAtLoginState.Enabled, service.GetState());
    }

    /// <summary>
    /// What a moved or reinstalled app leaves behind. Reported as Disabled so
    /// the startup reconciler rewrites it rather than trusting a dead path.
    /// </summary>
    [Fact]
    public void GetState_WithAnEntryPointingElsewhere_IsDisabled()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Build(@"C:\Old\Location\Parlotype.exe").SetEnabled(true);

        Assert.Equal(LaunchAtLoginState.Disabled, Build().GetState());
    }

    [Fact]
    public void GetState_IgnoresCasingDifferencesInTheRegisteredPath()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Windows paths are case-insensitive; a casing change is not a stale entry.
        Build(FakeExe.ToUpperInvariant()).SetEnabled(true);

        Assert.Equal(LaunchAtLoginState.Enabled, Build().GetState());
    }

    [Theory]
    // Byte 0's low bit is the disabled flag: 02/06 enabled, 03 disabled.
    [InlineData(0x03, LaunchAtLoginState.BlockedByOperatingSystem)]
    [InlineData(0x02, LaunchAtLoginState.Enabled)]
    [InlineData(0x06, LaunchAtLoginState.Enabled)]
    public void GetState_ReadsTheWindowsStartupApprovalFlag(byte flag, LaunchAtLoginState expected)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var service = Build();
        service.SetEnabled(true);
        WriteApproval(flag);

        Assert.Equal(expected, service.GetState());
    }

    /// <summary>
    /// Writing the entry cannot clear an Explorer veto, and this app does not
    /// forge one — so the write reports failure and the UI explains why.
    /// </summary>
    [Fact]
    public void SetEnabled_WhenWindowsHasVetoedTheEntry_ReportsFailure()
    {
        if (!OperatingSystem.IsWindows())
            return;

        WriteApproval(0x03);

        Assert.False(Build().SetEnabled(true));
    }

    /// <summary>
    /// The veto only matters for an entry that exists. Nothing registered means
    /// nothing to block.
    /// </summary>
    [Fact]
    public void GetState_WithAVetoButNoEntry_IsDisabled()
    {
        if (!OperatingSystem.IsWindows())
            return;

        WriteApproval(0x03);

        Assert.Equal(LaunchAtLoginState.Disabled, Build().GetState());
    }

    [Fact]
    public void UnsupportedBuild_ReportsUnsupported_AndWritesNothing()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Null target = not installed by Setup.exe. Registering a path that is
        // about to move would leave a broken autorun entry behind.
        var service = Build(target: null);

        Assert.False(service.IsSupported);
        Assert.Equal(LaunchAtLoginState.Unsupported, service.GetState());
        Assert.False(service.SetEnabled(true));
        Assert.Null(Registry.CurrentUser.OpenSubKey(RunKeyPath));
    }

    /// <summary>
    /// The guarantee that a developer machine never picks up an autorun entry:
    /// built through the <em>production</em> constructor, so this exercises the
    /// real Velopack resolution in a build Velopack did not install.
    /// </summary>
    [Fact]
    public void ProductionConstructor_InANonInstalledBuild_IsUnsupported()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var service = new WindowsRunKeyLaunchAtLoginService(
            NullLogger<WindowsRunKeyLaunchAtLoginService>.Instance);

        Assert.False(service.IsSupported);
        Assert.Equal(LaunchAtLoginState.Unsupported, service.GetState());
        Assert.False(service.SetEnabled(true));
    }

    private void WriteApproval(byte flag)
    {
        if (!OperatingSystem.IsWindows())
            return;

        using var key = Registry.CurrentUser.CreateSubKey(ApprovedKeyPath, writable: true);
        var blob = new byte[12];
        blob[0] = flag;
        key!.SetValue(WindowsRunKeyLaunchAtLoginService.ValueName, blob, RegistryValueKind.Binary);
    }

    public void Dispose()
    {
        if (!OperatingSystem.IsWindows())
            return;

        try
        {
            Registry.CurrentUser.DeleteSubKeyTree(_root, throwOnMissingSubKey: false);

            // Leave nothing behind in the user's registry. Non-recursive, so it
            // throws (and is swallowed) while a parallel instance still has a
            // scratch key of its own — the last one out wins.
            Registry.CurrentUser.DeleteSubKey(ScratchParent, throwOnMissingSubKey: false);
        }
        catch
        {
            // A leftover scratch key under Software\Parlotype.Tests is harmless.
        }

        GC.SuppressFinalize(this);
    }
}
