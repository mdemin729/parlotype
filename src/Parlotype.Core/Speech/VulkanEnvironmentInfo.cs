namespace Parlotype.Core.Speech;

/// <summary>The kind of physical device exposed by a Vulkan implementation.</summary>
public enum VulkanDeviceType
{
    Other,
    IntegratedGpu,
    DiscreteGpu,
    VirtualGpu,
    Cpu,
}

/// <summary>
/// A single Vulkan-capable device reported by the loader.
/// </summary>
/// <param name="Name">Marketing name of the device (e.g. <c>"NVIDIA GeForce RTX 4070"</c>).</param>
/// <param name="DeviceType">Whether the device is discrete, integrated, etc.</param>
/// <param name="DriverVersion">Driver version string reported by the device.</param>
/// <param name="ApiVersion">Vulkan API version supported by the device (e.g. <c>"1.3.268"</c>).</param>
public sealed record VulkanDeviceInfo(
    string Name,
    VulkanDeviceType DeviceType,
    string DriverVersion,
    string ApiVersion);

/// <summary>
/// Snapshot of the Vulkan environment on the current machine.
/// All fields are best-effort — any individual field may be empty if the
/// corresponding source could not be queried.
/// </summary>
public sealed record VulkanEnvironmentInfo
{
    /// <summary>True when <c>vulkan-1</c> (the Vulkan loader) is loadable.</summary>
    public bool HasVulkanLoader { get; init; }

    /// <summary>
    /// Version of the Vulkan loader API as reported via
    /// <c>vkEnumerateInstanceVersion</c> (e.g. <c>"1.3.268"</c>), or <c>null</c>
    /// if the loader is unavailable or didn't report one.
    /// </summary>
    public string? LoaderVersion { get; init; }

    /// <summary>True if the <c>VULKAN_SDK</c> environment variable is set to an existing directory.</summary>
    public bool SdkInstalled { get; init; }

    /// <summary>Value of the <c>VULKAN_SDK</c> environment variable, when present.</summary>
    public string? SdkPath { get; init; }

    /// <summary>Vulkan-capable physical devices reported by the loader. Empty if none or detection failed.</summary>
    public IReadOnlyList<VulkanDeviceInfo> Devices { get; init; } = [];

    /// <summary>True when at least the loader is present — sufficient for Whisper.net to attempt Vulkan.</summary>
    public bool HasVulkan => HasVulkanLoader;

    /// <summary>An empty snapshot indicating no Vulkan environment was detected.</summary>
    public static VulkanEnvironmentInfo Empty { get; } = new();
}
