using Parlotype.Desktop.Services;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Every test uses a unique lock name so a Parlotype running on the developer's
/// machine cannot fail the run — and so the tests cannot collide with each
/// other when xUnit runs them in parallel.
/// </summary>
public class SingleInstanceGuardTests
{
    private static string UniqueName() => $"Parlotype.Tests.{Guid.NewGuid():N}";

    /// <summary>
    /// A mutex is owned by a thread, not a process, so a second acquisition on
    /// the calling thread would succeed recursively. Acquiring off-thread is
    /// what actually models a second process.
    /// </summary>
    private static SingleInstanceGuard AcquireOnAnotherThread(string name)
    {
        SingleInstanceGuard? guard = null;
        var thread = new Thread(() => guard = SingleInstanceGuard.Acquire(name));
        thread.Start();
        thread.Join();
        return guard!;
    }

    [Fact]
    public void Acquire_WhenNothingHoldsTheLock_IsPrimary()
    {
        using var guard = SingleInstanceGuard.Acquire(UniqueName());

        Assert.True(guard.IsPrimary);
    }

    [Fact]
    public void Acquire_WhileAnotherInstanceHoldsTheLock_IsNotPrimary()
    {
        var name = UniqueName();
        using var first = SingleInstanceGuard.Acquire(name);

        using var second = AcquireOnAnotherThread(name);

        Assert.True(first.IsPrimary);
        Assert.False(second.IsPrimary);
    }

    [Fact]
    public void Acquire_AfterTheHolderIsDisposed_IsPrimaryAgain()
    {
        var name = UniqueName();
        using (var first = SingleInstanceGuard.Acquire(name))
        {
            Assert.True(first.IsPrimary);
        }

        using var next = AcquireOnAnotherThread(name);

        Assert.True(next.IsPrimary);
    }

    /// <summary>
    /// The crash case: the owning process dies without releasing the mutex, which
    /// surfaces as <c>AbandonedMutexException</c> on the next wait. It must read
    /// as "the lock is free", not as a failure that leaves the app unstartable.
    /// </summary>
    [Fact]
    public void Acquire_AfterTheHolderVanished_IsPrimaryAgain()
    {
        var name = UniqueName();

        // Never disposed, and the owning thread exits — exactly what a killed
        // process leaves behind.
        var abandoned = new Thread(() => SingleInstanceGuard.Acquire(name));
        abandoned.Start();
        abandoned.Join();

        using var next = SingleInstanceGuard.Acquire(name);

        Assert.True(next.IsPrimary);
    }

    [Fact]
    public void SignalPrimary_FromTheSecondInstance_RaisesTheListener()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Named activation events are a Windows primitive.");

        var name = UniqueName();
        using var activated = new ManualResetEventSlim(initialState: false);

        using var primary = SingleInstanceGuard.Acquire(name);
        primary.ListenForActivation(() => activated.Set());

        using var second = AcquireOnAnotherThread(name);
        Assert.True(second.SignalPrimary());

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void SignalPrimary_FromThePrimaryItself_DoesNothing()
    {
        using var primary = SingleInstanceGuard.Acquire(UniqueName());

        Assert.False(primary.SignalPrimary());
    }

    /// <summary>
    /// A signal that arrives while the running instance is still starting up —
    /// before <see cref="SingleInstanceGuard.ListenForActivation"/> — must not be
    /// lost, or an impatient double-launch shows no window at all.
    /// </summary>
    [Fact]
    public void SignalPrimary_BeforeTheListenerStarts_IsDeliveredWhenItDoes()
    {
        Assert.SkipUnless(OperatingSystem.IsWindows(), "Named activation events are a Windows primitive.");

        var name = UniqueName();
        using var activated = new ManualResetEventSlim(initialState: false);

        using var primary = SingleInstanceGuard.Acquire(name);
        using var second = AcquireOnAnotherThread(name);
        Assert.True(second.SignalPrimary());

        primary.ListenForActivation(() => activated.Set());

        Assert.True(activated.Wait(TimeSpan.FromSeconds(5), TestContext.Current.CancellationToken));
    }
}
