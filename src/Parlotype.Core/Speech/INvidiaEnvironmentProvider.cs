namespace Parlotype.Core.Speech;

/// <summary>
/// Detects and reports the NVIDIA driver, CUDA toolkit, and CUDA runtime
/// available on the current machine. Independent of Whisper.net.
/// Implementations must be safe to call from a background thread and should
/// cache the first successful detection.
/// </summary>
public interface INvidiaEnvironmentProvider
{
    /// <summary>
    /// Returns the cached <see cref="NvidiaEnvironmentInfo"/> if available,
    /// otherwise runs detection and caches the result.
    /// Detection failures are absorbed — the returned info will simply have
    /// missing fields rather than throwing.
    /// </summary>
    Task<NvidiaEnvironmentInfo> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a fresh detection, replacing the cached value.
    /// Useful for "refresh" actions in diagnostics UI.
    /// </summary>
    Task<NvidiaEnvironmentInfo> RefreshAsync(CancellationToken cancellationToken = default);
}
