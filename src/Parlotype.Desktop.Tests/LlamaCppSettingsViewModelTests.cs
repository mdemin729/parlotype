using Parlotype.Core.Settings;
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

        Assert.Equal("llama.cpp server", vm.Title);
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
    public async Task SaveSettings_PersistsBothPortAndFolder()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        vm.PortText = "9999";
        vm.ServerFolder = @"C:\custom\llama";
        vm.SaveSettingsCommand.Execute(null);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        var port = await settings.GetAsync<string>("LlamaCppPort", TestContext.Current.CancellationToken);
        var folder = await settings.GetAsync<string>("LlamaCppServerFolder", TestContext.Current.CancellationToken);
        Assert.Equal("9999", port);
        Assert.Equal(@"C:\custom\llama", folder);
    }

    [Fact]
    public async Task SaveSettings_InvalidPort_ShowsError()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        vm.PortText = "invalid";
        vm.SaveSettingsCommand.Execute(null);

        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.NotNull(vm.ErrorMessage);
        Assert.Contains("1", vm.ErrorMessage);
    }

    [Fact]
    public async Task ResetDefaults_RestoresDefaultValues()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        vm.PortText = "9999";
        vm.ServerFolder = @"C:\custom\path";
        vm.ResetDefaultsCommand.Execute(null);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Equal("8321", vm.PortText);
        Assert.Equal("", vm.ServerFolder);
        Assert.Equal("", await settings.GetAsync<string>(
            SettingsKeys.LlamaCppServerFolder, TestContext.Current.CancellationToken));
    }


    [Fact]
    public async Task StoredPackFolderPath_IsFlaggedButLeftAlone()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Exactly what a real settings.json in the wild carries, left behind by the
        // old placeholder.
        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "llama-server");

        var settings = new MockSettingsService();
        await settings.SetAsync(
            SettingsKeys.LlamaCppServerFolder, legacy, TestContext.Current.CancellationToken);

        var vm = new LlamaCppSettingsViewModel(settings);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.True(vm.IsServerFolderInsidePackFolder);

        // Not migrated: repointing at parlotype-data would break a manual install
        // whose binaries really are at the old path (ADR-065).
        Assert.Equal(legacy, vm.ServerFolder);
        Assert.Equal(legacy, await settings.GetAsync<string>(
            SettingsKeys.LlamaCppServerFolder, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task UnconfiguredManualFolder_StaysEmpty()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);

        await Task.Delay(200, TestContext.Current.CancellationToken);

        // No default to pre-fill with (ADR-066): an empty box is the honest
        // rendering of "you have not chosen a manual build".
        Assert.Equal("", vm.ServerFolder);
        Assert.False(vm.IsServerFolderInsidePackFolder);
    }

    [Fact]
    public async Task SaveSettings_EmptyFolder_SavesPortWithoutError()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        // The regression this guards: with the box empty by default, rejecting an
        // empty folder would block every managed-install user from saving a port.
        vm.PortText = "9100";
        vm.ServerFolder = "";
        vm.SaveSettingsCommand.Execute(null);
        await Task.Delay(200, TestContext.Current.CancellationToken);

        Assert.Null(vm.ErrorMessage);
        Assert.Equal("9100", await settings.GetAsync<string>(
            SettingsKeys.LlamaCppPort, TestContext.Current.CancellationToken));
    }

    [Fact]
    public void EditingTheFolderBox_UpdatesTheWarningLive()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var vm = new LlamaCppSettingsViewModel(new MockSettingsService());

        vm.ServerFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Parlotype", "current");
        Assert.True(vm.IsServerFolderInsidePackFolder);

        vm.ServerFolder = @"C:\tools\llama";
        Assert.False(vm.IsServerFolderInsidePackFolder);
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
