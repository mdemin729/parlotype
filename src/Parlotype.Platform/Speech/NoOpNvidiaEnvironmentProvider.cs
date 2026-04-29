using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// No-op <see cref="INvidiaEnvironmentProvider"/> for non-Windows platforms.
/// Always returns <see cref="NvidiaEnvironmentInfo.Empty"/>.
/// </summary>
internal sealed class NoOpNvidiaEnvironmentProvider : INvidiaEnvironmentProvider
{
    public Task<NvidiaEnvironmentInfo> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(NvidiaEnvironmentInfo.Empty);

    public Task<NvidiaEnvironmentInfo> RefreshAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(NvidiaEnvironmentInfo.Empty);
}
