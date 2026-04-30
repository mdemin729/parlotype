using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Desktop.V2.Services;
using Parlotype.Desktop.V2.Tests.Mocks;
using Parlotype.Desktop.V2.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Parlotype.Desktop.V2.Tests;

public class HotkeyCoordinatorTests
{
    [AvaloniaFact]
    public async Task HotkeyPress_StartsCoordinator_AndShowsTranscribeWindow()
    {
        var hotkey = new MockGlobalHotkeyService();
        var wm = new MockWindowManager();
        var transcribeVm = new TranscribeViewModel(wm);

        var coordinator = new HotkeyCoordinator(
            wm,
            transcribeVm,
            NullLogger<HotkeyCoordinator>.Instance,
            hotkey);

        await coordinator.StartAsync(TestContext.Current.CancellationToken);
        Assert.True(hotkey.IsStarted);

        hotkey.SimulatePress();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        // Allow the posted async lambda to complete.
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.True(wm.ShowTranscribeCount >= 1);
        coordinator.Dispose();
    }
}
