using System.Diagnostics;
using Parlotype.Platform.Startup;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Smoke test for the <c>NtQueryInformationProcess</c> P/Invoke (ADR-062).
/// Windows-only; no-ops elsewhere, matching <c>WindowsNvidiaEnvironmentProviderTests</c>.
/// </summary>
public class NativeParentProcessTests
{
    [Fact]
    public void TryGetParentProcessId_OnWindows_ResolvesALiveProcess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var resolved = NativeParentProcess.TryGetParentProcessId(out var parentPid);

        Assert.True(resolved);
        Assert.True(parentPid > 0);

        // The test host was created by something that is still alive right now.
        var parent = Process.GetProcessById(parentPid);
        Assert.False(parent.HasExited);
    }

    [Fact]
    public void TryGetParentProcessId_DoesNotReturnTheCurrentProcess()
    {
        if (!OperatingSystem.IsWindows())
            return;

        Assert.True(NativeParentProcess.TryGetParentProcessId(out var parentPid));
        Assert.NotEqual(Environment.ProcessId, parentPid);
    }
}
