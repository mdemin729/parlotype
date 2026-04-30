using Parlotype.Desktop.V2.Tests.Mocks;
using Parlotype.Desktop.V2.ViewModels;
using Xunit;

namespace Parlotype.Desktop.V2.Tests;

public class AppViewModelTests
{
    [Fact]
    public void OpenCommand_InvokesShowTranscribe()
    {
        var wm = new MockWindowManager();
        var vm = new AppViewModel(wm);

        vm.OpenCommand.Execute(null);

        Assert.Equal(1, wm.ShowTranscribeCount);
    }

    [Fact]
    public void OpenSettingsCommand_InvokesShowSettings()
    {
        var wm = new MockWindowManager();
        var vm = new AppViewModel(wm);

        vm.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, wm.ShowSettingsCount);
    }

    [Fact]
    public void ExitCommand_InvokesExit()
    {
        var wm = new MockWindowManager();
        var vm = new AppViewModel(wm);

        vm.ExitCommand.Execute(null);

        Assert.Equal(1, wm.ExitCount);
    }

    [Fact]
    public void TrayIconClickedCommand_InvokesShowTranscribe()
    {
        var wm = new MockWindowManager();
        var vm = new AppViewModel(wm);

        vm.TrayIconClickedCommand.Execute(null);

        Assert.Equal(1, wm.ShowTranscribeCount);
    }
}
