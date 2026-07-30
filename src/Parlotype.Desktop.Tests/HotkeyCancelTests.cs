using Avalonia.Headless.XUnit;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The Escape / hold-abort discard path: recording stops, and whatever was
/// captured is thrown away instead of being transcribed and typed.
/// </summary>
public class HotkeyCancelTests
{
    private static async Task SettleAsync()
    {
        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => { });
        await Task.Delay(100);
    }

    [AvaloniaFact]
    public async Task Cancel_Stops_Recording_Without_Injecting()
    {
        var pipeline = new MockAudioPipeline();
        var injector = new MockTextInjectionService();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, injector);

        await vm.StartRecordingAsync();
        Assert.True(vm.IsRecording);

        await vm.CancelRecordingAsync();

        Assert.False(vm.IsRecording);
        Assert.Equal(1, pipeline.StopCount);
        Assert.Equal("Cancelled", vm.StatusText);

        // A transcription that lands after the cancel has nowhere to go.
        pipeline.RaiseTranscriptionAvailable("discard me");
        await SettleAsync();

        Assert.Empty(injector.InjectedTexts);
    }

    [AvaloniaFact]
    public async Task Cancel_While_Idle_Is_A_No_Op()
    {
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, new MockTextInjectionService());

        await vm.CancelRecordingAsync();

        Assert.Equal(0, pipeline.StopCount);
        Assert.False(vm.IsRecording);
    }

    [AvaloniaFact]
    public async Task Cancel_Does_Not_Wait_For_An_In_Flight_Start()
    {
        // Escape during a cold model load must release the UI at once — waiting
        // out a load that can run for seconds is what a *stop* does (ADR-039),
        // not a cancel.
        var pipeline = new MockAudioPipeline { StartDelay = TimeSpan.FromSeconds(3) };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, new MockTextInjectionService());

        var start = vm.StartRecordingAsync();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        await vm.CancelRecordingAsync();
        sw.Stop();

        Assert.True(sw.ElapsedMilliseconds < 500,
            $"cancel blocked for {sw.ElapsedMilliseconds} ms waiting on the model load");
        Assert.False(vm.IsRecording);
        Assert.Equal("Cancelled", vm.StatusText);

        await start;
    }

    [AvaloniaFact]
    public async Task Start_Finishing_After_A_Cancel_Discards_Instead_Of_Recording()
    {
        // The load cannot be interrupted, so the pipeline does start — it just
        // has to be torn straight back down without ever entering the recording
        // state or injecting anything.
        var pipeline = new MockAudioPipeline { StartDelay = TimeSpan.FromMilliseconds(300) };
        var injector = new MockTextInjectionService();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, injector);

        var start = vm.StartRecordingAsync();
        await vm.CancelRecordingAsync();
        await start;
        await SettleAsync();

        Assert.False(vm.IsRecording);
        Assert.Equal(1, pipeline.StopCount);
        Assert.Equal("Cancelled", vm.StatusText);

        pipeline.RaiseTranscriptionAvailable("discard me");
        await SettleAsync();
        Assert.Empty(injector.InjectedTexts);
    }

    [AvaloniaFact]
    public async Task A_Later_Start_Is_Unaffected_By_An_Earlier_Cancel()
    {
        var pipeline = new MockAudioPipeline { StartDelay = TimeSpan.FromMilliseconds(200) };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, new MockTextInjectionService());

        var start = vm.StartRecordingAsync();
        await vm.CancelRecordingAsync();
        await start;

        pipeline.StartDelay = null;
        await vm.StartRecordingAsync();

        Assert.True(vm.IsRecording);
        Assert.Equal("Recording...", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task Coordinator_Routes_Cancel_To_The_Discard_Path()
    {
        var hotkey = new MockGlobalHotkeyService();
        var pipeline = new MockAudioPipeline();
        var injector = new MockTextInjectionService();
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm, pipeline, injector);

        using var coordinator = new HotkeyCoordinator(
            wm, vm, NullLogger<HotkeyCoordinator>.Instance, hotkey);

        await coordinator.StartAsync(TestContext.Current.CancellationToken);

        hotkey.SimulateStart();
        await SettleAsync();
        Assert.True(vm.IsRecording);

        hotkey.SimulateCancel();
        await SettleAsync();

        Assert.False(vm.IsRecording);
        pipeline.RaiseTranscriptionAvailable("discard me");
        await SettleAsync();
        Assert.Empty(injector.InjectedTexts);
    }

    [AvaloniaFact]
    public async Task Coordinator_Reports_Recording_State_To_The_Hotkey_Service()
    {
        // Toggle gestures and Escape both depend on this being accurate.
        var hotkey = new MockGlobalHotkeyService();
        var pipeline = new MockAudioPipeline();
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm, pipeline, new MockTextInjectionService());

        using var coordinator = new HotkeyCoordinator(
            wm, vm, NullLogger<HotkeyCoordinator>.Instance, hotkey);

        await coordinator.StartAsync(TestContext.Current.CancellationToken);
        Assert.False(hotkey.DictationActive);

        hotkey.SimulateStart();
        await SettleAsync();
        Assert.True(hotkey.DictationActive);

        hotkey.SimulateStop();
        await SettleAsync();
        Assert.False(hotkey.DictationActive);
    }

    [AvaloniaFact]
    public async Task Recording_Started_From_The_Button_Also_Reports_Active()
    {
        // The hook never sees the widget's own button, so without the state
        // feedback a toggle gesture could not stop it.
        var hotkey = new MockGlobalHotkeyService();
        var pipeline = new MockAudioPipeline();
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm, pipeline, new MockTextInjectionService());

        using var coordinator = new HotkeyCoordinator(
            wm, vm, NullLogger<HotkeyCoordinator>.Instance, hotkey);

        await coordinator.StartAsync(TestContext.Current.CancellationToken);

        await vm.StartRecordingAsync();
        await SettleAsync();

        Assert.True(hotkey.DictationActive);
    }

    [AvaloniaFact]
    public async Task Coordinator_Publishes_The_Hotkey_Hint()
    {
        var hotkey = new MockGlobalHotkeyService();
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm);

        using var coordinator = new HotkeyCoordinator(
            wm, vm, NullLogger<HotkeyCoordinator>.Instance, hotkey);

        await coordinator.StartAsync(TestContext.Current.CancellationToken);
        await SettleAsync();

        Assert.Equal("Hold Right Ctrl to talk · Esc to cancel", vm.HotkeyHintText);

        hotkey.UpdateBindings([DictationHotkeyDefaults.Toggle]);
        await SettleAsync();

        Assert.Equal("Double-tap Ctrl to dictate · Esc to cancel", vm.HotkeyHintText);
    }
}
