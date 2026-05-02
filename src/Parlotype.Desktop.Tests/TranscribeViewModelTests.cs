using Avalonia.Headless.XUnit;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class TranscribeViewModelTests
{
    [AvaloniaFact]
    public void OpenSettings_InvokesWindowManager()
    {
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm);

        vm.OpenSettingsCommand.Execute(null);

        Assert.Equal(1, wm.ShowSettingsCount);
    }

    [AvaloniaFact]
    public async Task TogglePlay_NoPipeline_LeavesNotRecording()
    {
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm);

        await vm.TogglePlayCommand.ExecuteAsync(null);

        Assert.False(vm.IsRecording);
        Assert.Equal("Ready", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task StartRecording_SetsIsRecordingAndStatus()
    {
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        await vm.StartRecordingAsync();

        Assert.True(vm.IsRecording);
        Assert.Equal("Recording...", vm.StatusText);
        Assert.Equal(1, pipeline.StartCount);
    }

    [AvaloniaFact]
    public async Task TranscriptionAvailable_InjectsText()
    {
        var pipeline = new MockAudioPipeline();
        var injector = new MockTextInjectionService();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, injector);

        await vm.StartRecordingAsync();
        pipeline.RaiseTranscriptionAvailable("hello world");

        // Allow the async void handler to complete
        await Task.Delay(100);

        Assert.Single(injector.InjectedTexts);
        Assert.Equal("hello world", injector.InjectedTexts[0]);
    }

    [AvaloniaFact]
    public async Task TranscriptionAvailable_EmptyText_SkipsInjection()
    {
        var pipeline = new MockAudioPipeline();
        var injector = new MockTextInjectionService();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, injector);

        await vm.StartRecordingAsync();
        pipeline.RaiseTranscriptionAvailable("   ");

        await Task.Delay(100);

        Assert.Empty(injector.InjectedTexts);
    }

    [AvaloniaFact]
    public async Task StopRecording_UnsubscribesFromPipeline()
    {
        var pipeline = new MockAudioPipeline();
        var injector = new MockTextInjectionService();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, injector);

        await vm.StartRecordingAsync();
        await vm.StopRecordingAsync();

        // Event fired after stop should not reach the injector
        pipeline.RaiseTranscriptionAvailable("should not arrive");
        await Task.Delay(100);

        Assert.False(vm.IsRecording);
        Assert.Equal("Ready", vm.StatusText);
        Assert.Empty(injector.InjectedTexts);
    }

    [AvaloniaFact]
    public async Task StartRecording_PipelineThrows_ResetsState()
    {
        var pipeline = new MockAudioPipeline
        {
            ThrowOnStart = new InvalidOperationException("mic unavailable")
        };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        await vm.StartRecordingAsync();

        Assert.False(vm.IsRecording);
        Assert.Equal("Ready", vm.StatusText);
    }
}
