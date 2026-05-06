using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Windows implementation of <see cref="IVulkanEnvironmentProvider"/>.
/// Probes the Vulkan loader (<c>vulkan-1.dll</c>), reads the loader API version,
/// enumerates physical devices, and reports SDK presence via <c>VULKAN_SDK</c>.
/// All probing is isolated so any single failure leaves the rest intact.
/// </summary>
internal sealed class WindowsVulkanEnvironmentProvider : IVulkanEnvironmentProvider
{
    private readonly ILogger<WindowsVulkanEnvironmentProvider> _logger;
    private readonly SemaphoreSlim _gate = new(1, 1);
    private VulkanEnvironmentInfo? _cached;

    public WindowsVulkanEnvironmentProvider(ILogger<WindowsVulkanEnvironmentProvider> logger)
    {
        _logger = logger;
    }

    public async Task<VulkanEnvironmentInfo> GetAsync(CancellationToken cancellationToken = default)
    {
        if (_cached is not null)
            return _cached;

        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            return _cached ??= Detect();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task<VulkanEnvironmentInfo> RefreshAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            _cached = Detect();
            return _cached;
        }
        finally
        {
            _gate.Release();
        }
    }

    private VulkanEnvironmentInfo Detect()
    {
        var sdkPath = Environment.GetEnvironmentVariable("VULKAN_SDK");
        var sdkInstalled = !string.IsNullOrEmpty(sdkPath) && Directory.Exists(sdkPath);

        if (!NativeLibrary.TryLoad("vulkan-1", out var handle))
        {
            _logger.LogDebug("vulkan-1 loader could not be loaded — Vulkan unavailable");
            return new VulkanEnvironmentInfo
            {
                HasVulkanLoader = false,
                SdkInstalled = sdkInstalled,
                SdkPath = sdkPath,
            };
        }

        try
        {
            var loaderVersion = TryGetLoaderVersion(handle);
            var devices = TryEnumerateDevices(handle);
            return new VulkanEnvironmentInfo
            {
                HasVulkanLoader = true,
                LoaderVersion = loaderVersion,
                SdkInstalled = sdkInstalled,
                SdkPath = sdkPath,
                Devices = devices,
            };
        }
        finally
        {
            NativeLibrary.Free(handle);
        }
    }

    // ----- Loader version --------------------------------------------------

    private string? TryGetLoaderVersion(IntPtr handle)
    {
        try
        {
            // vkEnumerateInstanceVersion was added in Vulkan 1.1; absence implies a 1.0 loader.
            if (!NativeLibrary.TryGetExport(handle, "vkEnumerateInstanceVersion", out var exportPtr))
                return "1.0.0";

            var fn = Marshal.GetDelegateForFunctionPointer<VkEnumerateInstanceVersionDelegate>(exportPtr);
            var status = fn(out var packed);
            return status == 0 ? DecodeVulkanVersion(packed) : null;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "vkEnumerateInstanceVersion probe failed");
            return null;
        }
    }

    // ----- Physical device enumeration -------------------------------------

    private IReadOnlyList<VulkanDeviceInfo> TryEnumerateDevices(IntPtr handle)
    {
        IntPtr instance = IntPtr.Zero;
        try
        {
            if (!NativeLibrary.TryGetExport(handle, "vkCreateInstance", out var createPtr))
                return [];
            if (!NativeLibrary.TryGetExport(handle, "vkDestroyInstance", out _))
                return [];
            if (!NativeLibrary.TryGetExport(handle, "vkEnumeratePhysicalDevices", out var enumPtr))
                return [];
            if (!NativeLibrary.TryGetExport(handle, "vkGetPhysicalDeviceProperties", out var propsPtr))
                return [];

            var vkCreateInstance = Marshal.GetDelegateForFunctionPointer<VkCreateInstanceDelegate>(createPtr);
            var vkEnumeratePhysicalDevices = Marshal.GetDelegateForFunctionPointer<VkEnumeratePhysicalDevicesDelegate>(enumPtr);
            var vkGetPhysicalDeviceProperties = Marshal.GetDelegateForFunctionPointer<VkGetPhysicalDevicePropertiesDelegate>(propsPtr);

            var info = new VkInstanceCreateInfo { sType = VkStructureTypeInstanceCreateInfo };
            var status = vkCreateInstance(in info, IntPtr.Zero, out instance);
            if (status != 0 || instance == IntPtr.Zero)
            {
                _logger.LogDebug("vkCreateInstance returned {Status} — no Vulkan devices reported", status);
                return [];
            }

            uint deviceCount = 0;
            status = vkEnumeratePhysicalDevices(instance, ref deviceCount, IntPtr.Zero);
            if (status != 0 || deviceCount == 0)
                return [];

            var deviceHandles = new IntPtr[deviceCount];
            var handlesGc = GCHandle.Alloc(deviceHandles, GCHandleType.Pinned);
            try
            {
                status = vkEnumeratePhysicalDevices(instance, ref deviceCount, handlesGc.AddrOfPinnedObject());
            }
            finally
            {
                handlesGc.Free();
            }
            if (status != 0)
                return [];

            var buffer = Marshal.AllocHGlobal(VkPhysicalDevicePropertiesSize);
            try
            {
                var devices = new List<VulkanDeviceInfo>((int)deviceCount);
                for (var i = 0; i < deviceCount; i++)
                {
                    vkGetPhysicalDeviceProperties(deviceHandles[i], buffer);
                    devices.Add(ReadDeviceInfo(buffer));
                }
                return devices;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Vulkan physical device enumeration failed");
            return [];
        }
        finally
        {
            if (instance != IntPtr.Zero
                && NativeLibrary.TryGetExport(handle, "vkDestroyInstance", out var destroyPtr))
            {
                try
                {
                    var vkDestroyInstance = Marshal.GetDelegateForFunctionPointer<VkDestroyInstanceDelegate>(destroyPtr);
                    vkDestroyInstance(instance, IntPtr.Zero);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "vkDestroyInstance failed");
                }
            }
        }
    }

    private static VulkanDeviceInfo ReadDeviceInfo(IntPtr buffer)
    {
        // VkPhysicalDeviceProperties layout (head only, the rest is unused here):
        //   offset  0: uint32 apiVersion
        //   offset  4: uint32 driverVersion
        //   offset  8: uint32 vendorID
        //   offset 12: uint32 deviceID
        //   offset 16: int32 deviceType
        //   offset 20: char deviceName[256] (UTF-8, null-terminated)
        var apiVersion = (uint)Marshal.ReadInt32(buffer, 0);
        var driverVersion = (uint)Marshal.ReadInt32(buffer, 4);
        var deviceType = Marshal.ReadInt32(buffer, 16);

        var nameBytes = new byte[VkMaxPhysicalDeviceNameSize];
        Marshal.Copy(buffer + 20, nameBytes, 0, VkMaxPhysicalDeviceNameSize);
        var nullIndex = Array.IndexOf(nameBytes, (byte)0);
        if (nullIndex < 0) nullIndex = nameBytes.Length;
        var name = Encoding.UTF8.GetString(nameBytes, 0, nullIndex);

        return new VulkanDeviceInfo(
            string.IsNullOrEmpty(name) ? "Unknown" : name,
            DecodeDeviceType(deviceType),
            DecodeDriverVersion(driverVersion),
            DecodeVulkanVersion(apiVersion));
    }

    // ----- Encoding helpers ------------------------------------------------

    /// <summary>
    /// Decodes a packed Vulkan API version into "major.minor.patch".
    /// Layout: variant (top 3 bits, ignored), major (7), minor (10), patch (12).
    /// </summary>
    internal static string DecodeVulkanVersion(uint packed)
    {
        var major = (packed >> 22) & 0x7FU;
        var minor = (packed >> 12) & 0x3FFU;
        var patch = packed & 0xFFFU;
        return string.Create(CultureInfo.InvariantCulture, $"{major}.{minor}.{patch}");
    }

    /// <summary>
    /// Driver version encoding is vendor-specific. We render it as a hex literal
    /// (matching what tools like vulkaninfo show as a fallback) when no canonical
    /// breakdown is available.
    /// </summary>
    internal static string DecodeDriverVersion(uint packed)
        => string.Create(CultureInfo.InvariantCulture, $"0x{packed:X8}");

    internal static VulkanDeviceType DecodeDeviceType(int raw) => raw switch
    {
        1 => VulkanDeviceType.IntegratedGpu,
        2 => VulkanDeviceType.DiscreteGpu,
        3 => VulkanDeviceType.VirtualGpu,
        4 => VulkanDeviceType.Cpu,
        _ => VulkanDeviceType.Other,
    };

    // ----- Native interop --------------------------------------------------

    private const int VkMaxPhysicalDeviceNameSize = 256;
    private const int VkPhysicalDevicePropertiesSize = 824;
    private const uint VkStructureTypeInstanceCreateInfo = 1;

    [StructLayout(LayoutKind.Sequential)]
    private struct VkInstanceCreateInfo
    {
        public uint sType;
        public IntPtr pNext;
        public uint flags;
        public IntPtr pApplicationInfo;
        public uint enabledLayerCount;
        public IntPtr ppEnabledLayerNames;
        public uint enabledExtensionCount;
        public IntPtr ppEnabledExtensionNames;
    }

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VkEnumerateInstanceVersionDelegate(out uint apiVersion);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VkCreateInstanceDelegate(in VkInstanceCreateInfo createInfo, IntPtr allocator, out IntPtr instance);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VkDestroyInstanceDelegate(IntPtr instance, IntPtr allocator);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate int VkEnumeratePhysicalDevicesDelegate(IntPtr instance, ref uint count, IntPtr devices);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void VkGetPhysicalDevicePropertiesDelegate(IntPtr device, IntPtr properties);
}
