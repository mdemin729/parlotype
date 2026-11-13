namespace Parlotype.Core.LlamaServer;

/// <summary>
/// Fetches the catalog of installable llama.cpp server builds from an upstream
/// source (GitHub releases). Implementations are expected to cache results to
/// stay under unauthenticated rate limits and to filter variants to the current
/// OS / architecture.
/// </summary>
public interface ILlamaServerCatalog
{
    /// <summary>
    /// Returns the build that upstream's newest versioned release names first,
    /// marked with <see cref="LlamaServerReleaseGroup.ReleaseVersion"/>, when it
    /// is known, and then the other builds newest first. No build appears twice.
    /// When the released build cannot be determined the result is simply the
    /// newest builds, none of them marked (ADR-074). When
    /// <paramref name="forceRefresh"/> is false, implementations may serve a
    /// cached snapshot.
    /// </summary>
    Task<IReadOnlyList<LlamaServerReleaseGroup>> FetchAsync(
        bool forceRefresh,
        CancellationToken cancellationToken = default);
}
