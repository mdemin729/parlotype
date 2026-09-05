using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Desktop.Services;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Behaviour of the development-only parent-process watchdog (ADR-062). Uses the
/// internal test seam so no real process is spawned.
/// </summary>
public class ParentProcessExitWatcherTests
{
    private static ParentProcessExitWatcher Build(
        bool installed,
        ParentProcessRef? parent,
        TimeSpan forceExitDelay,
        Action onForceExit) =>
        new(
            NullLogger<ParentProcessExitWatcher>.Instance,
            isInstalledBuild: () => installed,
            resolveParent: () => parent,
            forceExitDelay: forceExitDelay,
            forceExit: onForceExit);

    private static ParentProcessRef ParentThatExitsWhen(Task exited) =>
        new(Id: 4242, Name: "dotnet", WaitForExitAsync: ct => exited.WaitAsync(ct));

    [Fact]
    public async Task InstalledBuild_NeverRequestsShutdown()
    {
        var exit = new TaskCompletionSource();
        var shutdownCalls = 0;
        var forceExitCalls = 0;

        using var watcher = Build(
            installed: true,
            ParentThatExitsWhen(exit.Task),
            TimeSpan.FromMilliseconds(20),
            () => Interlocked.Increment(ref forceExitCalls));

        watcher.Start(() => Interlocked.Increment(ref shutdownCalls));
        exit.SetResult();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, shutdownCalls);
        Assert.Equal(0, forceExitCalls);
    }

    [Fact]
    public async Task UnresolvedParent_NeverRequestsShutdown()
    {
        var shutdownCalls = 0;

        using var watcher = Build(
            installed: false,
            parent: null,
            TimeSpan.FromMilliseconds(20),
            onForceExit: () => Assert.Fail("force-exit must not run"));

        watcher.Start(() => Interlocked.Increment(ref shutdownCalls));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, shutdownCalls);
    }

    [Fact]
    public async Task LauncherExits_RequestsShutdownExactlyOnce()
    {
        var exit = new TaskCompletionSource();
        var shutdownCalls = 0;

        // Simulate a graceful shutdown: the real Exit handler disposes the watcher.
        ParentProcessExitWatcher? watcher = null;
        watcher = Build(
            installed: false,
            ParentThatExitsWhen(exit.Task),
            TimeSpan.FromSeconds(30),
            onForceExit: () => Assert.Fail("graceful path — force-exit must not run"));

        watcher.Start(() =>
        {
            Interlocked.Increment(ref shutdownCalls);
            watcher!.Dispose();
        });

        exit.SetResult();
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal(1, shutdownCalls);
        watcher.Dispose();
    }

    [Fact]
    public async Task GracefulShutdownStalls_ForcesExit()
    {
        var exit = new TaskCompletionSource();
        var forceExit = new TaskCompletionSource();

        using var watcher = Build(
            installed: false,
            ParentThatExitsWhen(exit.Task),
            TimeSpan.FromMilliseconds(50),
            onForceExit: () => forceExit.TrySetResult());

        // requestShutdown does nothing — nobody disposes the watcher.
        watcher.Start(() => { });
        exit.SetResult();

        var finished = await Task.WhenAny(
            forceExit.Task,
            Task.Delay(2000, TestContext.Current.CancellationToken));

        Assert.Same(forceExit.Task, finished);
    }

    [Fact]
    public async Task DisposedBeforeLauncherExits_DoesNothing()
    {
        var exit = new TaskCompletionSource();
        var shutdownCalls = 0;

        var watcher = Build(
            installed: false,
            ParentThatExitsWhen(exit.Task),
            TimeSpan.FromMilliseconds(20),
            onForceExit: () => Assert.Fail("force-exit must not run"));

        watcher.Start(() => Interlocked.Increment(ref shutdownCalls));
        watcher.Dispose();
        exit.SetResult();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, shutdownCalls);
    }
}
