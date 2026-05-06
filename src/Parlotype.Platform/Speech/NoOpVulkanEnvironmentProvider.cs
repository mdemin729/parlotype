using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// No-op <see cref="IVulkanEnvironmentProvider"/> for platforms where detection
/// hasn't been implemented. Always returns <see cref="VulkanEnvironmentInfo.Empty"/>.
/// </summary>
internal sealed class NoOpVulkanEnvironmentProvider : IVulkanEnvironmentProvider
{
    public Task<VulkanEnvironmentInfo> GetAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(VulkanEnvironmentInfo.Empty);

    public Task<VulkanEnvironmentInfo> RefreshAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(VulkanEnvironmentInfo.Empty);
}
