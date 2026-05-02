using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Avalonia.Headless.XUnit;
using Xunit;

namespace Parlotype.Desktop.Tests;

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

    [AvaloniaFact]
    public async Task FullFlow_Press_Transcription_Injection_Release()
    {
        var hotkey = new MockGlobalHotkeyService();
        var wm = new MockWindowManager();
        var pipeline = new MockAudioPipeline();
        var injector = new MockTextInjectionService();
        var transcribeVm = new TranscribeViewModel(wm, pipeline, injector);

        using var coordinator = new HotkeyCoordinator(
            wm,
            transcribeVm,
            NullLogger<HotkeyCoordinator>.Instance,
            hotkey);

        await coordinator.StartAsync(TestContext.Current.CancellationToken);

        // Simulate hotkey press → starts recording
        hotkey.SimulatePress();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.True(transcribeVm.IsRecording);
        Assert.Equal("Recording...", transcribeVm.StatusText);

        // Pipeline produces a transcription → text is injected
        pipeline.RaiseTranscriptionAvailable("hello from voice");
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Single(injector.InjectedTexts);
        Assert.Equal("hello from voice", injector.InjectedTexts[0]);

        // Simulate hotkey release → stops recording
        hotkey.SimulateRelease();
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.False(transcribeVm.IsRecording);
        Assert.Equal("Ready", transcribeVm.StatusText);

        // Events after stop should not reach the injector
        pipeline.RaiseTranscriptionAvailable("should not arrive");
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Single(injector.InjectedTexts);
    }
}
