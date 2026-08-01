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
        bool hasVulkan = true)
    {
        var vulkan = new MockVulkanEnvironmentProvider(hasVulkan
            ? new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" }
            : VulkanEnvironmentInfo.Empty);
        return new RuntimeSettingsViewModel(settings, vulkan);
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

    /// <summary>
    /// Settings written before ADR-049 can still name the removed CUDA runtime. The
    /// selection must fall back to Auto <i>and</i> the stale value must be rewritten,
    /// so it never resurfaces in a later session or a bug report.
    /// </summary>
    [Fact]
    public async Task Initialize_MigratesRemovedCudaPreference_ToAuto()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.RuntimePreference, "Cuda", TestContext.Current.CancellationToken);

        var vm = Build(settings);
        await SettleAsync();

        Assert.Equal(RuntimePreference.Auto, vm.SelectedRuntime);
        var saved = await settings.GetAsync<string>(SettingsKeys.RuntimePreference, TestContext.Current.CancellationToken);
        Assert.Equal("Auto", saved);
    }

    [Fact]
    public async Task RuntimeOptions_DoNotOfferCuda()
    {
        var settings = new MockSettingsService();
        var vm = Build(settings);
        await SettleAsync();

        Assert.Equal(
            [RuntimePreference.Auto, RuntimePreference.Vulkan, RuntimePreference.Cpu],
            vm.RuntimeOptions.Select(o => o.Type));
    }

    [Fact]
    public async Task Availability_ReflectsEnvironmentDetection()
    {
        var settings = new MockSettingsService();
        var vm = Build(settings, hasVulkan: true);
        await SettleAsync();

        var auto = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Auto);
        var vulkan = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Vulkan);
        var cpu = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Cpu);

        Assert.True(auto.IsAvailable);
        Assert.True(vulkan.IsAvailable);
        Assert.True(cpu.IsAvailable);
        Assert.False(vm.VulkanLoaderMissing);
    }

    [Fact]
    public async Task Availability_VulkanMissing_FlagsLoaderMissing()
    {
        var settings = new MockSettingsService();
        var vm = Build(settings, hasVulkan: false);
        await SettleAsync();

        var vulkan = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Vulkan);

        Assert.False(vulkan.IsAvailable);
        Assert.True(vm.VulkanLoaderMissing);
        Assert.NotNull(vulkan.UnavailableReason);

        // Auto and Cpu stay selectable — both can still run on the CPU backend.
        Assert.True(vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Auto).IsAvailable);
        Assert.True(vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Cpu).IsAvailable);
    }

    [Fact]
    public async Task RestartRequired_WhenSelectionDiffersFromLoadedRuntime()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.RuntimePreference, RuntimePreference.Vulkan.ToString(), TestContext.Current.CancellationToken);
        var vm = BuildWithStatus(settings, new FakeRuntimeStatus("Vulkan"));
        await SettleAsync();

        Assert.False(vm.RestartRequired);
        Assert.Equal("Vulkan", vm.LoadedRuntimeName);

        vm.SelectRuntimeCommand.Execute(RuntimePreference.Cpu);

        Assert.True(vm.RestartRequired);
    }

    [Fact]
    public async Task RestartRequired_IsFalse_WhenNoRuntimeLoadedYet()
    {
        var settings = new MockSettingsService();
        var vm = BuildWithStatus(settings, new FakeRuntimeStatus(loaded: null));
        await SettleAsync();

        vm.SelectRuntimeCommand.Execute(RuntimePreference.Vulkan);

        Assert.False(vm.RestartRequired);
        Assert.Null(vm.LoadedRuntimeName);
    }

    private static RuntimeSettingsViewModel BuildWithStatus(
        MockSettingsService settings,
        IWhisperRuntimeStatus status)
    {
        var vulkan = new MockVulkanEnvironmentProvider(
            new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" });
        return new RuntimeSettingsViewModel(settings, vulkan, status);
    }

    /// <summary>Mimics the process-wide runtime latch without loading anything native.</summary>
    private sealed class FakeRuntimeStatus(string? loaded) : IWhisperRuntimeStatus
    {
        public string? LoadedRuntimeName => loaded;

        public bool RequiresRestartFor(RuntimePreference preference) => loaded switch
        {
            null => false,
            _ => preference is not RuntimePreference.Auto && preference.ToString() != loaded,
        };
    }
}
