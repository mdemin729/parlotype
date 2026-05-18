using Parlotype.Core.LlamaServer;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>Catalog stub that returns a scripted list of release groups.</summary>
public sealed class MockLlamaServerCatalog : ILlamaServerCatalog
{
    private readonly List<LlamaServerReleaseGroup> _groups = new();

    public int FetchCallCount { get; private set; }
    public int ForceRefreshCount { get; private set; }
    public Exception? FetchError { get; set; }

    public void SetGroups(params LlamaServerReleaseGroup[] groups)
    {
        _groups.Clear();
        _groups.AddRange(groups);
    }

    public Task<IReadOnlyList<LlamaServerReleaseGroup>> FetchAsync(
        bool forceRefresh, CancellationToken cancellationToken = default)
    {
        FetchCallCount++;
        if (forceRefresh) ForceRefreshCount++;
        if (FetchError is not null)
            return Task.FromException<IReadOnlyList<LlamaServerReleaseGroup>>(FetchError);
        return Task.FromResult<IReadOnlyList<LlamaServerReleaseGroup>>(_groups.ToList());
    }
}
