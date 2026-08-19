using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// End-to-end plumbing for hold-scoped push-to-talk (ADR-060): the gesture's scope
/// has to survive the trip from the hotkey service through the coordinator and the
/// view model to the pipeline, because nothing downstream can re-derive it.
/// </summary>
public class HotkeyHoldScopedTests
{
    private static (MockGlobalHotkeyService Hotkey, MockAudioPipeline Pipeline, HotkeyCoordinator Coordinator)
        Build()
    {
        var hotkey = new MockGlobalHotkeyService();
        var wm = new MockWindowManager();
        var pipeline = new MockAudioPipeline();
        var transcribeVm = new TranscribeViewModel(wm, pipeline, new MockTextInjectionService());

        var coordinator = new HotkeyCoordinator(
            wm, transcribeVm, NullLogger<HotkeyCoordinator>.Instance, hotkey);

        return (hotkey, pipeline, coordinator);
    }

    private static async Task SettleAsync()
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        await Task.Delay(100, TestContext.Current.CancellationToken);
    }

    [AvaloniaFact]
    public async Task HoldGesture_StartsPipelineInSingleUtteranceMode()
    {
        var (hotkey, pipeline, coordinator) = Build();
        using var _ = coordinator;

        await coordinator.StartAsync(TestContext.Current.CancellationToken);

        hotkey.SimulateStart(holdScoped: true);
        await SettleAsync();

        Assert.Equal(PipelineMode.SingleUtterance, pipeline.LastStartMode);
    }

    [AvaloniaFact]
    public async Task ToggleGesture_StartsPipelineInBatchMode()
    {
        var (hotkey, pipeline, coordinator) = Build();
        using var _ = coordinator;

        await coordinator.StartAsync(TestContext.Current.CancellationToken);

        hotkey.SimulateStart(holdScoped: false);
        await SettleAsync();

        // A toggle session has no release to wait for, so silence stays the only
        // cue that a sentence ended.
        Assert.Equal(PipelineMode.Batch, pipeline.LastStartMode);
    }

    [AvaloniaFact]
    public async Task RecordButton_StartsPipelineInBatchMode()
    {
        var wm = new MockWindowManager();
        var pipeline = new MockAudioPipeline();
        var transcribeVm = new TranscribeViewModel(wm, pipeline, new MockTextInjectionService());

        // The widget's own button, with no gesture behind it.
        await transcribeVm.StartRecordingAsync();

        Assert.Equal(PipelineMode.Batch, pipeline.LastStartMode);
    }
}
