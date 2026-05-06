using Parlotype.Core.Speech;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockVulkanEnvironmentProvider(VulkanEnvironmentInfo? info = null) : IVulkanEnvironmentProvider
{
    private readonly VulkanEnvironmentInfo _info = info ?? VulkanEnvironmentInfo.Empty;
    public Task<VulkanEnvironmentInfo> GetAsync(CancellationToken ct = default) => Task.FromResult(_info);
    public Task<VulkanEnvironmentInfo> RefreshAsync(CancellationToken ct = default) => Task.FromResult(_info);
}
