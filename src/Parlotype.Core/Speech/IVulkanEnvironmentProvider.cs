namespace Parlotype.Core.Speech;

/// <summary>
/// Detects and reports the Vulkan loader, SDK, and physical devices available
/// on the current machine. Independent of Whisper.net.
/// Implementations must be safe to call from a background thread and should
/// cache the first successful detection.
/// </summary>
public interface IVulkanEnvironmentProvider
{
    /// <summary>
    /// Returns the cached <see cref="VulkanEnvironmentInfo"/> if available,
    /// otherwise runs detection and caches the result.
    /// Detection failures are absorbed — the returned info will simply have
    /// missing fields rather than throwing.
    /// </summary>
    Task<VulkanEnvironmentInfo> GetAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Forces a fresh detection, replacing the cached value.
    /// Useful for "refresh" actions in diagnostics UI.
    /// </summary>
    Task<VulkanEnvironmentInfo> RefreshAsync(CancellationToken cancellationToken = default);
}
