namespace Parlotype.Core.Speech.LlamaServer;

/// <summary>
/// Fetches the catalog of installable llama.cpp server builds from an upstream
/// source (GitHub releases). Implementations are expected to cache results to
/// stay under unauthenticated rate limits and to filter variants to the current
/// OS / architecture.
/// </summary>
public interface ILlamaServerCatalog
{
    /// <summary>
    /// Returns release groups newest-first. When <paramref name="forceRefresh"/>
    /// is false, implementations may serve a cached snapshot.
    /// </summary>
    Task<IReadOnlyList<LlamaServerReleaseGroup>> FetchAsync(
        bool forceRefresh,
        CancellationToken cancellationToken = default);
}
