using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class WhisperModelSettingsViewModelTests
{
    [Fact]
    public async Task SelectModel_UpdatesSelectionAndPersists()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperModelSettingsViewModel(settings);

        vm.SelectModelCommand.Execute(WhisperModelType.Small);
        await Task.Yield();

        Assert.Equal(WhisperModelType.Small, vm.SelectedModel);
        Assert.True(vm.ModelOptions.First(m => m.Type == WhisperModelType.Small).IsSelected);
        Assert.False(vm.ModelOptions.First(m => m.Type == WhisperModelType.Base).IsSelected);
    }

    [Fact]
    public async Task SelectModel_StopsRecordingWhenActive()
    {
        var settings = new MockSettingsService();
        var pipeline = new MockAudioPipeline();
        var windowManager = new MockWindowManager();
        var recognizer = new MockSpeechRecognizer();
        var transcribeVm = new TranscribeViewModel(windowManager, pipeline);

        await transcribeVm.StartRecordingAsync();
        Assert.True(transcribeVm.IsRecording);

        var vm = new WhisperModelSettingsViewModel(settings, transcribeViewModel: transcribeVm, recognizer: recognizer);

        vm.SelectModelCommand.Execute(WhisperModelType.Medium);
        await Task.Yield();

        Assert.False(transcribeVm.IsRecording);
        Assert.Equal(1, pipeline.StopCount);
    }

    [Fact]
    public async Task SelectModel_UnloadsModelWhenReady()
    {
        var settings = new MockSettingsService();
        var recognizer = new MockSpeechRecognizer();
        await recognizer.InitializeAsync(TestContext.Current.CancellationToken);
        Assert.True(recognizer.IsReady);

        var vm = new WhisperModelSettingsViewModel(settings, recognizer: recognizer);

        vm.SelectModelCommand.Execute(WhisperModelType.LargeV3);
        await Task.Yield();

        Assert.False(recognizer.IsReady);
        Assert.Equal(1, recognizer.UnloadCount);
    }

    [Fact]
    public async Task SelectModel_SameModel_DoesNothing()
    {
        var settings = new MockSettingsService();
        var recognizer = new MockSpeechRecognizer();
        await recognizer.InitializeAsync(TestContext.Current.CancellationToken);

        var vm = new WhisperModelSettingsViewModel(settings, recognizer: recognizer);

        vm.SelectModelCommand.Execute(WhisperModelType.Base);
        await Task.Yield();

        Assert.True(recognizer.IsReady);
        Assert.Equal(0, recognizer.UnloadCount);
    }
}
