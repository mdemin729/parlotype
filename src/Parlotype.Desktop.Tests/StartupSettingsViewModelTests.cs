using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Platform.Startup;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The launch-at-sign-in toggle (ADR-059). The switch must reflect what the OS
/// will actually do, not merely what was stored.
/// </summary>
public class StartupSettingsViewModelTests
{
    private static StartupSettingsViewModel Build(
        MockLaunchAtLoginService launchAtLogin,
        MockSettingsService? settings = null) =>
        new(new LaunchAtLoginCoordinator(
            settings ?? new MockSettingsService(),
            launchAtLogin,
            NullLogger<LaunchAtLoginCoordinator>.Instance));

    /// <summary>
    /// The VM's load is fire-and-forget, like every other settings section.
    /// </summary>
    private static async Task SettleAsync() => await Task.Delay(50);

    [Fact]
    public async Task WithNothingStored_ShowsOn()
    {
        var vm = Build(new MockLaunchAtLoginService());
        await SettleAsync();

        Assert.True(vm.LaunchAtLogin);
        Assert.True(vm.IsSupported);
        Assert.False(vm.IsBlockedByWindows);
    }

    [Fact]
    public async Task WithAnExplicitOptOut_ShowsOff()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.LaunchAtLogin, "False", TestContext.Current.CancellationToken);

        var vm = Build(new MockLaunchAtLoginService(), settings);
        await SettleAsync();

        Assert.False(vm.LaunchAtLogin);
    }

    [Fact]
    public async Task TurningItOff_PersistsThePreferenceAndUnregisters()
    {
        var settings = new MockSettingsService();
        var launchAtLogin = new MockLaunchAtLoginService(registered: true);
        var vm = Build(launchAtLogin, settings);
        await SettleAsync();

        vm.LaunchAtLogin = false;
        await vm.PendingWrite;

        Assert.Equal(
            "False",
            await settings.GetAsync<string>(SettingsKeys.LaunchAtLogin, TestContext.Current.CancellationToken));
        Assert.Equal(LaunchAtLoginState.Disabled, launchAtLogin.GetState());
        Assert.False(vm.LaunchAtLogin);
    }

    [Fact]
    public async Task TurningItBackOn_Registers()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.LaunchAtLogin, "False", TestContext.Current.CancellationToken);
        var launchAtLogin = new MockLaunchAtLoginService();
        var vm = Build(launchAtLogin, settings);
        await SettleAsync();

        vm.LaunchAtLogin = true;
        await vm.PendingWrite;

        Assert.Equal(LaunchAtLoginState.Enabled, launchAtLogin.GetState());
        Assert.True(vm.LaunchAtLogin);
    }

    /// <summary>
    /// The refresh that follows a write must not loop back through the change
    /// handler and write again.
    /// </summary>
    [Fact]
    public async Task ApplyingAChange_DoesNotWriteTheRegistrationTwice()
    {
        var launchAtLogin = new MockLaunchAtLoginService(registered: true);
        var vm = Build(launchAtLogin);
        await SettleAsync();

        vm.LaunchAtLogin = false;
        await vm.PendingWrite;
        await SettleAsync();

        Assert.Equal(1, launchAtLogin.SetCount);
    }

    [Fact]
    public async Task OnAnUnsupportedBuild_ShowsOffWithAnExplanation()
    {
        var vm = Build(new MockLaunchAtLoginService(isSupported: false));
        await SettleAsync();

        Assert.False(vm.IsSupported);
        Assert.False(vm.LaunchAtLogin);
        Assert.Contains("Setup.exe", vm.StatusText);
    }

    /// <summary>
    /// A Windows-side veto keeps the switch showing the stored preference — the
    /// user did ask for this — while the warning carries the bad news, since
    /// only Task Manager can lift it.
    /// </summary>
    [Fact]
    public async Task WhenWindowsHasVetoedTheEntry_ExplainsWhereToFixIt()
    {
        var launchAtLogin = new MockLaunchAtLoginService(registered: true)
        {
            ForcedState = LaunchAtLoginState.BlockedByOperatingSystem,
        };
        var vm = Build(launchAtLogin);
        await SettleAsync();

        Assert.True(vm.IsBlockedByWindows);
        Assert.Contains("Task Manager", vm.StatusText);
        Assert.True(vm.LaunchAtLogin);
    }
}
