using Parlotype.Desktop.V2.Tests.Mocks;
using Parlotype.Desktop.V2.ViewModels;
using Xunit;

namespace Parlotype.Desktop.V2.Tests;

public class TranscribeViewModelTests
{
    [Fact]
    public void OpenSettings_InvokesWindowManager()
    {
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm);

        vm.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, wm.ShowSettingsCount);
    }

    [Fact]
    public async Task TogglePlay_NoPipeline_LeavesNotRecording()
    {
        // With no pipeline registered, toggle should be a no-op (defensive).
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm);

        await vm.TogglePlayCommand.ExecuteAsync(null);

        Assert.False(vm.IsRecording);
        Assert.Equal("Ready", vm.StatusText);
    }
}
