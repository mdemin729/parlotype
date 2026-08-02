using Parlotype.Core.Updates;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// In-memory <see cref="IUpdateService"/>. Defaults to
/// <see cref="UpdateState.NotInstalled"/> — the state headless tests actually run
/// in — and never touches the network.
/// </summary>
public sealed class MockUpdateService : IUpdateService
{
    public UpdateStatus Status { get; private set; } = new(UpdateState.NotInstalled);

    public event EventHandler<UpdateStatus>? StatusChanged;

    public string? CurrentVersion { get; set; }

    public int CheckCount { get; private set; }

    public bool ApplyCalled { get; private set; }

    /// <summary>What the next <see cref="CheckAsync"/> should report.</summary>
    public UpdateStatus? NextCheckResult { get; set; }

    public Task StartAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task<UpdateStatus> CheckAsync(bool userInitiated, CancellationToken cancellationToken = default)
    {
        CheckCount++;
        if (NextCheckResult is { } next)
            Publish(next);

        return Task.FromResult(Status);
    }

    /// <summary>Number of times the shutdown path handed a staged update to the updater.</summary>
    public int ApplyOnExitCount { get; private set; }

    public bool ApplyOnExit()
    {
        ApplyOnExitCount++;
        return Status.State == UpdateState.ReadyToApply;
    }

    public Task<bool> ApplyAndRestartAsync(CancellationToken cancellationToken = default)
    {
        ApplyCalled = true;
        return Task.FromResult(Status.State == UpdateState.ReadyToApply);
    }

    /// <summary>Test seam: drives a status transition as the real service would.</summary>
    public void Publish(UpdateStatus status)
    {
        Status = status;
        StatusChanged?.Invoke(this, status);
    }
}
