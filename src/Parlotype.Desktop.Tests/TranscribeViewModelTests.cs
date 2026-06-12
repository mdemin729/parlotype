using Avalonia.Headless.XUnit;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
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
        Assert.Equal(RecordingState.Disabled, vm.RecordingState);
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
        Assert.Equal(RecordingState.Idle, vm.RecordingState);
    }

    [AvaloniaFact]
    public async Task StartRecording_SetsRecordingStateToIdle()
    {
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        Assert.Equal(RecordingState.Disabled, vm.RecordingState);

        await vm.StartRecordingAsync();

        Assert.Equal(RecordingState.Idle, vm.RecordingState);
    }

    [AvaloniaFact]
    public async Task StopRecording_SetsRecordingStateToDisabled()
    {
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        await vm.StartRecordingAsync();
        Assert.Equal(RecordingState.Idle, vm.RecordingState);

        await vm.StopRecordingAsync();
        Assert.Equal(RecordingState.Disabled, vm.RecordingState);
        Assert.Equal(0f, vm.AudioLevel);
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
    public async Task StartRecording_ShowsLoadingStatus_DuringPipelineStart()
    {
        var pipeline = new MockAudioPipeline
        {
            StartDelay = TimeSpan.FromMilliseconds(200)
        };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        var startTask = vm.StartRecordingAsync();

        // While pipeline is still starting, status should show loading message
        Assert.Equal("Loading model...", vm.StatusText);
        Assert.False(vm.IsRecording);

        await startTask;

        // After pipeline started, status should show recording
        Assert.Equal("Recording...", vm.StatusText);
        Assert.True(vm.IsRecording);
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
        Assert.Equal(RecordingState.Disabled, vm.RecordingState);
    }

    [AvaloniaFact]
    public async Task StopRecording_UnsubscribesFromAudioLevel()
    {
        var pipeline = new MockAudioPipeline();
        var levelProvider = new MockAudioLevelProvider();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, audioLevelProvider: levelProvider);

        await vm.StartRecordingAsync();
        await vm.StopRecordingAsync();

        // Level event after stop should not change state
        levelProvider.RaiseLevelChanged(0.5f);
        await Task.Delay(50);

        Assert.Equal(RecordingState.Disabled, vm.RecordingState);
        Assert.Equal(0f, vm.AudioLevel);
    }

    [AvaloniaFact]
    public async Task AudioLevel_AboveThreshold_SetsActiveState()
    {
        var pipeline = new MockAudioPipeline();
        var levelProvider = new MockAudioLevelProvider();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, audioLevelProvider: levelProvider);

        await vm.StartRecordingAsync();
        Assert.Equal(RecordingState.Idle, vm.RecordingState);

        // Simulate speech
        levelProvider.RaiseLevelChanged(0.1f);
        await Task.Delay(50);

        Assert.Equal(RecordingState.Active, vm.RecordingState);
    }

    [AvaloniaFact]
    public async Task AudioLevel_BelowThreshold_HoldsActiveState()
    {
        var pipeline = new MockAudioPipeline();
        var levelProvider = new MockAudioLevelProvider();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, audioLevelProvider: levelProvider);

        await vm.StartRecordingAsync();

        // Go Active
        levelProvider.RaiseLevelChanged(0.1f);
        await Task.Delay(50);
        Assert.Equal(RecordingState.Active, vm.RecordingState);

        // Drop below threshold — should still be Active due to hold-off
        levelProvider.RaiseLevelChanged(0.0f);
        await Task.Delay(50);
        Assert.Equal(RecordingState.Active, vm.RecordingState);
    }

    [AvaloniaFact]
    public async Task AudioLevel_BelowThreshold_EventuallyGoesIdle()
    {
        var pipeline = new MockAudioPipeline();
        var levelProvider = new MockAudioLevelProvider();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, audioLevelProvider: levelProvider);

        await vm.StartRecordingAsync();

        // Go Active
        levelProvider.RaiseLevelChanged(0.1f);
        await Task.Delay(50);
        Assert.Equal(RecordingState.Active, vm.RecordingState);

        // Decay smoothed RMS with many zero events (slow decay factor 0.05)
        for (int i = 0; i < 100; i++)
        {
            levelProvider.RaiseLevelChanged(0.0f);
            await Task.Delay(5);
        }

        // Wait past the hold-off (1200ms)
        await Task.Delay(1300);

        // Final zero event to trigger the hold-off expiry check
        levelProvider.RaiseLevelChanged(0.0f);
        await Task.Delay(50);

        Assert.Equal(RecordingState.Idle, vm.RecordingState);
    }
}

/// <summary>
/// Phase 2: the quick-picker strip and language flyout on the Transcribe window
/// (spec §2.2, FR-W1..W5). Recording/audio behaviour stays covered above.
/// </summary>
public class TranscribeLanguageStripTests
{
    private static async Task<(TranscribeViewModel Vm, LanguageRelationshipViewModel Relationship,
        MockSettingsService Settings, MockWindowManager Wm)> CreateAsync(
        SpeechEngine engine = SpeechEngine.Whisper,
        KeyboardLayoutInfo? layout = null,
        MockAudioPipeline? pipeline = null)
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, engine.ToString(), TestContext.Current.CancellationToken);
        var relationship = new LanguageRelationshipViewModel(
            settings, new MockKeyboardLayoutService { Result = layout });
        await relationship.InitializeAsync(TestContext.Current.CancellationToken);

        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm, pipeline, relationship: relationship);
        return (vm, relationship, settings, wm);
    }

    [AvaloniaFact]
    public void NoRelationship_HidesStrip()
    {
        var vm = new TranscribeViewModel(new MockWindowManager());

        Assert.False(vm.HasLanguageStrip);
        Assert.Null(vm.TargetPicker);
        Assert.Equal("", vm.SourceShort);
    }

    [AvaloniaFact]
    public async Task Strip_ReflectsRelationshipAtRest()
    {
        var (vm, relationship, _, _) = await CreateAsync();

        Assert.True(vm.HasLanguageStrip);
        Assert.Equal("Auto", vm.SourceShort);
        Assert.Equal("Auto", vm.TargetShort); // off → output mirrors spoken
        Assert.False(relationship.IsConnectorOn);
    }

    [AvaloniaFact]
    public async Task Strip_ShowsTargetName_WhenTranslating()
    {
        var (vm, relationship, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        relationship.SelectSource("ru");
        relationship.SelectTarget("fr");

        Assert.Equal("Russian", vm.SourceShort);
        Assert.Equal("French", vm.TargetShort);
    }

    [AvaloniaFact]
    public async Task Strip_KeyboardSource_ShowsDetectedLanguage()
    {
        var (vm, relationship, _, _) = await CreateAsync(
            layout: new KeyboardLayoutInfo("de", "German (Germany)"));
        relationship.SelectSource(LanguageCatalog.KeyboardLayoutCode);

        Assert.Equal("German", vm.SourceShort);
    }

    [AvaloniaFact]
    public async Task StripConnector_TogglesTranslation_OneClick()
    {
        var (vm, relationship, settings, _) = await CreateAsync();

        vm.ToggleTranslationCommand.Execute(null);

        Assert.True(relationship.TranslationEnabled);
        Assert.Equal("English", vm.TargetShort);
        Assert.Equal(true.ToString(),
            await settings.GetAsync<string>(SettingsKeys.TranslationEnabled, TestContext.Current.CancellationToken));
    }

    [AvaloniaFact]
    public async Task Flyout_OpensWithFreshPickerState()
    {
        var (vm, _, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.TargetPicker!.Filter = "stale";

        vm.OpenLanguageFlyoutCommand.Execute(null);

        Assert.True(vm.IsLanguageFlyoutOpen);
        Assert.Equal("", vm.TargetPicker.Filter);
        // Off row leads the full-form picker.
        Assert.Equal(LanguageCatalog.NoTranslationCode, vm.TargetPicker.Items[0].Code);
    }

    [AvaloniaFact]
    public async Task Flyout_SelectTarget_EnablesTranslation_AndCloses()
    {
        var (vm, relationship, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.OpenLanguageFlyoutCommand.Execute(null);

        vm.TargetPicker!.SelectCommand.Execute("fr");

        Assert.False(vm.IsLanguageFlyoutOpen);
        Assert.True(relationship.TranslationEnabled);
        Assert.Equal("fr", relationship.TargetCode);
        Assert.Equal("French", vm.TargetShort);
    }

    [AvaloniaFact]
    public async Task Flyout_SourceRow_RoutesToSettings_AndCloses()
    {
        var (vm, _, _, wm) = await CreateAsync();
        vm.OpenLanguageFlyoutCommand.Execute(null);

        vm.GoToLanguageSettingsCommand.Execute(null);

        Assert.False(vm.IsLanguageFlyoutOpen);
        Assert.Equal(1, wm.ShowSettingsCount);
    }

    [AvaloniaFact]
    public async Task RelationshipChange_StopsActiveRecording()
    {
        var pipeline = new MockAudioPipeline();
        var (vm, relationship, _, _) = await CreateAsync(pipeline: pipeline);
        await vm.StartRecordingAsync();
        Assert.True(vm.IsRecording);

        // Change from any surface (here: as if the Settings page selected a source).
        relationship.SelectSource("ru");
        await Task.Delay(100);

        Assert.False(vm.IsRecording);
    }

    [AvaloniaFact]
    public async Task EngineSwitch_LocksStripConnector()
    {
        var (vm, relationship, _, _) = await CreateAsync();

        relationship.SetEngine((SpeechEngine)999);

        Assert.True(relationship.IsConnectorLocked);
        Assert.Equal("=", relationship.ConnectorGlyph);
        Assert.Equal(vm.SourceShort, vm.TargetShort); // output mirrors spoken
    }
}
