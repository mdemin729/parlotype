using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class RuntimeSettingsViewModelTests
{
    private static RuntimeSettingsViewModel Build(
        MockSettingsService settings,
        bool hasNvidia = false,
        bool hasVulkan = true)
    {
        var nvidia = new MockNvidiaEnvironmentProvider(hasNvidia
            ? new NvidiaEnvironmentInfo { DriverVersion = "999.99" }
            : NvidiaEnvironmentInfo.Empty);
        var vulkan = new MockVulkanEnvironmentProvider(hasVulkan
            ? new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" }
            : VulkanEnvironmentInfo.Empty);
        return new RuntimeSettingsViewModel(settings, nvidia, vulkan);
    }

    private static Task SettleAsync() => Task.Delay(50, TestContext.Current.CancellationToken);

    [Fact]
    public async Task Initialize_DefaultsToAuto_WhenSettingMissing()
    {
        var settings = new MockSettingsService();
        var vm = Build(settings);

        await SettleAsync();

        Assert.Equal(RuntimePreference.Auto, vm.SelectedRuntime);
    }

    [Fact]
    public async Task Initialize_ReadsSavedPreference()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.RuntimePreference, RuntimePreference.Vulkan.ToString(), TestContext.Current.CancellationToken);

        var vm = Build(settings);
        await SettleAsync();

        Assert.Equal(RuntimePreference.Vulkan, vm.SelectedRuntime);
    }

    [Fact]
    public async Task SelectRuntime_UpdatesSelectionAndPersists()
    {
        var settings = new MockSettingsService();
        var vm = Build(settings);
        await SettleAsync();

        vm.SelectRuntimeCommand.Execute(RuntimePreference.Cpu);
        await Task.Delay(20, TestContext.Current.CancellationToken);

        Assert.Equal(RuntimePreference.Cpu, vm.SelectedRuntime);
        var saved = await settings.GetAsync<string>(SettingsKeys.RuntimePreference, TestContext.Current.CancellationToken);
        Assert.Equal("Cpu", saved);
    }

    [Fact]
    public async Task Availability_ReflectsEnvironmentDetection()
    {
        var settings = new MockSettingsService();
        var vm = Build(settings, hasNvidia: false, hasVulkan: true);
        await SettleAsync();

        var cuda = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Cuda);
        var vulkan = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Vulkan);
        var cpu = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Cpu);

        Assert.False(cuda.IsAvailable);
        Assert.True(vulkan.IsAvailable);
        Assert.True(cpu.IsAvailable);
        Assert.NotNull(cuda.UnavailableReason);
        Assert.False(vm.VulkanLoaderMissing);
    }

    [Fact]
    public async Task Availability_VulkanMissing_FlagsLoaderMissing()
    {
        var settings = new MockSettingsService();
        var vm = Build(settings, hasNvidia: true, hasVulkan: false);
        await SettleAsync();

        var vulkan = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Vulkan);

        Assert.False(vulkan.IsAvailable);
        Assert.True(vm.VulkanLoaderMissing);
        Assert.NotNull(vulkan.UnavailableReason);
    }

    [Fact]
    public async Task CudaDriverMissing_WhenNoNvidiaDetected()
    {
        var settings = new MockSettingsService();
        var vm = Build(settings, hasNvidia: false);
        await SettleAsync();

        Assert.True(vm.CudaDriverMissing);
        Assert.False(vm.CudaSdkMissing);
        Assert.Null(vm.CudaDriverVersion);

        var cuda = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Cuda);
        Assert.False(cuda.IsAvailable);
        Assert.Contains("No NVIDIA GPU detected", cuda.UnavailableReason);
    }

    [Fact]
    public async Task CudaSdkMissing_WhenDriverPresentButNoRuntime()
    {
        var settings = new MockSettingsService();
        var nvidia = new MockNvidiaEnvironmentProvider(new NvidiaEnvironmentInfo
        {
            DriverVersion = "596.36",
            DriverMaxCudaVersion = "13.2",
            LoadableRuntimes = [],
        });
        var vulkan = new MockVulkanEnvironmentProvider(
            new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" });
        var vm = new RuntimeSettingsViewModel(settings, nvidia, vulkan);
        await SettleAsync();

        Assert.False(vm.CudaDriverMissing);
        Assert.True(vm.CudaSdkMissing);
        Assert.Equal("596.36", vm.CudaDriverVersion);

        var cuda = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Cuda);
        Assert.False(cuda.IsAvailable);
        Assert.Contains("CUDA toolkit not installed", cuda.UnavailableReason);
    }

    [Fact]
    public async Task CudaReady_WhenRuntimesLoadable()
    {
        var settings = new MockSettingsService();
        var nvidia = new MockNvidiaEnvironmentProvider(new NvidiaEnvironmentInfo
        {
            DriverVersion = "596.36",
            LoadableRuntimes = [new CudaRuntimeProbe("cudart64_13", "13.2", "13.2")],
        });
        var vulkan = new MockVulkanEnvironmentProvider(
            new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" });
        var vm = new RuntimeSettingsViewModel(settings, nvidia, vulkan);
        await SettleAsync();

        Assert.False(vm.CudaDriverMissing);
        Assert.False(vm.CudaSdkMissing);

        var cuda = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Cuda);
        Assert.True(cuda.IsAvailable);
        Assert.Null(cuda.UnavailableReason);
    }
}
