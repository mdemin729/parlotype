using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public sealed class WindowsVulkanEnvironmentProviderTests
{
    // Vulkan version packing: variant<<29 | major<<22 | minor<<12 | patch
    [Theory]
    [InlineData(0x4002DCu, "1.0.732")]   // VK_MAKE_API_VERSION(0, 1, 0, 732)
    [InlineData(0x403000u, "1.3.0")]     // VK_MAKE_API_VERSION(0, 1, 3, 0)
    [InlineData(0x40310Cu, "1.3.268")]   // VK_MAKE_API_VERSION(0, 1, 3, 268)
    public void DecodeVulkanVersion_DecodesPackedFields(uint packed, string expected)
    {
        Assert.Equal(expected, WindowsVulkanEnvironmentProvider.DecodeVulkanVersion(packed));
    }

    [Theory]
    [InlineData(0, VulkanDeviceType.Other)]
    [InlineData(1, VulkanDeviceType.IntegratedGpu)]
    [InlineData(2, VulkanDeviceType.DiscreteGpu)]
    [InlineData(3, VulkanDeviceType.VirtualGpu)]
    [InlineData(4, VulkanDeviceType.Cpu)]
    [InlineData(99, VulkanDeviceType.Other)]
    public void DecodeDeviceType_MapsRawValues(int raw, VulkanDeviceType expected)
    {
        Assert.Equal(expected, WindowsVulkanEnvironmentProvider.DecodeDeviceType(raw));
    }

    [Fact]
    public void DecodeDriverVersion_RendersAsHex()
    {
        Assert.Equal("0x12AB34CD", WindowsVulkanEnvironmentProvider.DecodeDriverVersion(0x12AB34CDu));
    }

    [Fact]
    public async Task GetAsync_ReturnsCachedSnapshot_WithoutThrowing()
    {
        // Smoke test: real provider on the test host. We don't assert presence of
        // Vulkan — only that detection completes and caches.
        var provider = new WindowsVulkanEnvironmentProvider(NullLogger<WindowsVulkanEnvironmentProvider>.Instance);

        var first = await provider.GetAsync();
        var second = await provider.GetAsync();

        Assert.Same(first, second);
    }
}
