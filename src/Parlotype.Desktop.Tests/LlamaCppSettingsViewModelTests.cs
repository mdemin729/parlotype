using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class LlamaCppSettingsViewModelTests
{
    [Fact]
    public void Title_IsLlamaCpp()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        Assert.Equal("llama.cpp", vm.Title);
    }

    [Fact]
    public void DefaultPort_Is8321()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        Assert.Equal("8321", vm.PortText);
    }

    [Fact]
    public void InitialStatus_IsNotProbed()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        Assert.Equal("Not probed", vm.StatusText);
        Assert.False(vm.IsConnected);
        Assert.False(vm.HasPortConflict);
    }

    [Fact]
    public async Task SavePort_PersistsToSettings()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        vm.PortText = "9999";
        vm.SavePortCommand.Execute(null);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var saved = await settings.GetAsync<string>("LlamaCppPort", TestContext.Current.CancellationToken);
        Assert.Equal("9999", saved);
    }

    [Fact]
    public async Task SavePort_InvalidPort_ShowsError()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        vm.PortText = "invalid";
        vm.SavePortCommand.Execute(null);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("1", vm.ErrorMessage);
    }

    [Fact]
    public async Task SaveServerPath_PersistsToSettings()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        vm.ServerPath = @"C:\custom\llama-server.exe";
        vm.SaveServerPathCommand.Execute(null);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var saved = await settings.GetAsync<string>("LlamaCppServerPath", TestContext.Current.CancellationToken);
        Assert.Equal(@"C:\custom\llama-server.exe", saved);
    }

    [Fact]
    public async Task RefreshServerInfo_NoServer_ShowsDisconnected()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        // Use a port unlikely to have anything running
        vm.PortText = "19999";
        vm.RefreshServerInfoCommand.Execute(null);

        await Task.Delay(6000, TestContext.Current.CancellationToken);

        Assert.Equal("Disconnected", vm.StatusText);
        Assert.False(vm.IsConnected);
    }
}
