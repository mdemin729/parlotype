using Parlotype.Core.Speech;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockNvidiaEnvironmentProvider(NvidiaEnvironmentInfo? info = null) : INvidiaEnvironmentProvider
{
    private readonly NvidiaEnvironmentInfo _info = info ?? NvidiaEnvironmentInfo.Empty;
    public Task<NvidiaEnvironmentInfo> GetAsync(CancellationToken ct = default) => Task.FromResult(_info);
    public Task<NvidiaEnvironmentInfo> RefreshAsync(CancellationToken ct = default) => Task.FromResult(_info);
}
