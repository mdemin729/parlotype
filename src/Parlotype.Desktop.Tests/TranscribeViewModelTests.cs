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
    public void ActiveEngine_DefaultsToParakeet_CloudIndicatorHidden()
    {
        var vm = new TranscribeViewModel(new MockWindowManager());

        Assert.Equal(SpeechEngine.Parakeet, vm.ActiveEngine);
        Assert.False(vm.IsCloudEngineActive);
        Assert.Null(vm.CloudProviderLabel);
    }

    [AvaloniaFact]
    public void SetActiveEngine_LocalEngines_CloudIndicatorHidden()
    {
        var vm = new TranscribeViewModel(new MockWindowManager());

        vm.SetActiveEngine(SpeechEngine.Whisper);
        Assert.False(vm.IsCloudEngineActive);
        Assert.Null(vm.CloudProviderLabel);

        vm.SetActiveEngine(SpeechEngine.Gemma4);
        Assert.False(vm.IsCloudEngineActive);
        Assert.Null(vm.CloudProviderLabel);
    }

    [AvaloniaFact]
    public void SetActiveEngine_OpenAiCompatible_ShowsCloudIndicator()
    {
        var vm = new TranscribeViewModel(new MockWindowManager());

        vm.SetActiveEngine(SpeechEngine.OpenAiCompatible);

        Assert.True(vm.IsCloudEngineActive);
        Assert.Equal("Cloud: OpenAI-compatible", vm.CloudProviderLabel);
    }

    [AvaloniaFact]
    public void SetActiveEngine_XaiGrok_ShowsCloudIndicator()
    {
        var vm = new TranscribeViewModel(new MockWindowManager());

        vm.SetActiveEngine(SpeechEngine.XaiGrok);

        Assert.True(vm.IsCloudEngineActive);
        Assert.Equal("Cloud: xAI Grok", vm.CloudProviderLabel);
    }

    [AvaloniaFact]
    public void SetActiveEngine_SwitchingBackToLocal_HidesCloudIndicator()
    {
        var vm = new TranscribeViewModel(new MockWindowManager());

        vm.SetActiveEngine(SpeechEngine.XaiGrok);
        Assert.True(vm.IsCloudEngineActive);

        vm.SetActiveEngine(SpeechEngine.Parakeet);

        Assert.False(vm.IsCloudEngineActive);
        Assert.Null(vm.CloudProviderLabel);
    }

    [AvaloniaFact]
    public async Task ActiveEngine_LoadsPersistedCloudEngine_WithoutSettingsWindow()
    {
        // Regression (ADR-032 commitment #3): the Transcribe window exists before
        // the Settings window is ever opened, so the badge must be correct from
        // the VM's own settings read — no SpeechEngineSettingsViewModel involved.
        var settings = new MockSettingsService();
        await settings.SetAsync(
            SettingsKeys.SpeechEngine, SpeechEngine.XaiGrok.ToString(),
            TestContext.Current.CancellationToken);

        var vm = new TranscribeViewModel(new MockWindowManager(), settings: settings);

        // InitializeActiveEngineAsync is fire-and-forget — give it a moment.
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(SpeechEngine.XaiGrok, vm.ActiveEngine);
        Assert.True(vm.IsCloudEngineActive);
        Assert.Equal("Cloud: xAI Grok", vm.CloudProviderLabel);
    }

    [AvaloniaFact]
    public async Task ActiveEngine_LoadsPersistedLocalEngine_BadgeStaysHidden()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(
            SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(),
            TestContext.Current.CancellationToken);

        var vm = new TranscribeViewModel(new MockWindowManager(), settings: settings);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(SpeechEngine.Whisper, vm.ActiveEngine);
        Assert.False(vm.IsCloudEngineActive);
        Assert.Null(vm.CloudProviderLabel);
    }

    [AvaloniaFact]
    public async Task ActiveEngine_LiveSelectionDuringInitialization_TakesPrecedence()
    {
        var settings = new DelayedSpeechEngineSettingsService(SpeechEngine.Parakeet.ToString());
        var vm = new TranscribeViewModel(new MockWindowManager(), settings: settings);
        await settings.WaitForReadAsync();

        vm.SetActiveEngine(SpeechEngine.XaiGrok);
        settings.CompleteRead();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(SpeechEngine.XaiGrok, vm.ActiveEngine);
        Assert.True(vm.IsCloudEngineActive);
        Assert.Equal("Cloud: xAI Grok", vm.CloudProviderLabel);
    }

    [AvaloniaFact]
    public async Task ActiveEngine_UnparsableSetting_FallsBackToParakeet()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(
            SettingsKeys.SpeechEngine, "NotARealEngine",
            TestContext.Current.CancellationToken);

        var vm = new TranscribeViewModel(new MockWindowManager(), settings: settings);
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(SpeechEngine.Parakeet, vm.ActiveEngine);
        Assert.False(vm.IsCloudEngineActive);
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
    public async Task StartRecording_ColdModel_ShowsSpinner()
    {
        var pipeline = new MockAudioPipeline
        {
            StartDelay = TimeSpan.FromMilliseconds(300)
        };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline)
        {
            LoadingSpinnerDelay = TimeSpan.FromMilliseconds(20)
        };

        var observed = new List<RecordingState>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TranscribeViewModel.RecordingState))
                observed.Add(vm.RecordingState);
        };

        await vm.StartRecordingAsync();

        // The load outlasted the spinner delay, so Loading must have been shown.
        Assert.Contains(RecordingState.Loading, observed);
        Assert.Equal("Recording...", vm.StatusText);
        Assert.True(vm.IsRecording);
        Assert.False(vm.IsLoading);
        Assert.Equal(RecordingState.Idle, vm.RecordingState);
    }

    [AvaloniaFact]
    public async Task StartRecording_HotModel_DoesNotFlashSpinner()
    {
        // Instant start (no StartDelay) simulates an already-loaded, hot model.
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        var observed = new List<RecordingState>();
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(TranscribeViewModel.RecordingState))
                observed.Add(vm.RecordingState);
        };

        await vm.StartRecordingAsync();

        // The hot start completed before the spinner delay — no Loading flicker.
        Assert.DoesNotContain(RecordingState.Loading, observed);
        Assert.Equal("Recording...", vm.StatusText);
        Assert.True(vm.IsRecording);
        Assert.False(vm.IsLoading);
        Assert.Equal(RecordingState.Idle, vm.RecordingState);
    }

    [AvaloniaFact]
    public async Task Prewarm_DelegatesToPipeline()
    {
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        await vm.PrewarmAsync();

        Assert.Equal(1, pipeline.PrewarmCount);
        // Prewarm must not start recording or alter the resting visual state.
        Assert.False(vm.IsRecording);
        Assert.False(vm.IsLoading);
        Assert.Equal(RecordingState.Disabled, vm.RecordingState);
    }

    [AvaloniaFact]
    public async Task Prewarm_NoPipeline_IsNoOp()
    {
        var vm = new TranscribeViewModel(new MockWindowManager());

        await vm.PrewarmAsync();

        Assert.False(vm.IsRecording);
        Assert.Equal(RecordingState.Disabled, vm.RecordingState);
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
    public async Task StartRecording_CloudProviderNotConfigured_ShowsDialogAndOpensSettings()
    {
        var pipeline = new MockAudioPipeline
        {
            ThrowOnStart = new CloudProviderNotConfiguredException(
                SpeechEngine.XaiGrok, "No API key configured for the xAI Grok provider.")
        };
        var wm = new MockWindowManager();
        var dialog = new MockUserDialogService { ConfirmationResult = true };
        var vm = new TranscribeViewModel(wm, pipeline, dialogService: dialog);

        await vm.StartRecordingAsync();
        await Task.Delay(100, TestContext.Current.CancellationToken); // dialog task is fire-and-forget

        Assert.False(vm.IsRecording);
        Assert.Equal(RecordingState.Disabled, vm.RecordingState);
        Assert.Equal("Cloud provider not configured", vm.StatusText);
        Assert.Equal(1, dialog.ShowConfirmationCount);
        Assert.Equal("Cloud provider not configured", dialog.LastTitle);
        Assert.Contains("No API key configured", dialog.LastMessage);
        Assert.Equal(1, wm.ShowSettingsCount);
        Assert.Equal(SettingsSection.CloudProviders, wm.LastSettingsSection);
    }

    [AvaloniaFact]
    public async Task StartRecording_CloudProviderNotConfigured_DialogCancelled_DoesNotOpenSettings()
    {
        var pipeline = new MockAudioPipeline
        {
            ThrowOnStart = new CloudProviderNotConfiguredException(
                SpeechEngine.OpenAiCompatible, "No API key configured for the OpenAI-compatible provider.")
        };
        var wm = new MockWindowManager();
        var dialog = new MockUserDialogService { ConfirmationResult = false };
        var vm = new TranscribeViewModel(wm, pipeline, dialogService: dialog);

        await vm.StartRecordingAsync();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(1, dialog.ShowConfirmationCount);
        Assert.Equal(0, wm.ShowSettingsCount);
    }

    [AvaloniaFact]
    public async Task TranscriptionFailed_QuotaExceeded_ShowsMessageDialog_RecordingContinues()
    {
        var pipeline = new MockAudioPipeline();
        var dialog = new MockUserDialogService();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, dialogService: dialog);
        await vm.StartRecordingAsync();

        pipeline.RaiseTranscriptionFailed(new CloudSpeechTranscriptionException(
            CloudSpeechErrorKind.QuotaExceeded,
            "OpenAI-compatible provider",
            "OpenAI-compatible provider: API quota exceeded — check your plan and billing with the provider."));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.True(vm.IsRecording); // informational only — recording keeps running
        Assert.Equal("Cloud quota exceeded — check plan & billing", vm.StatusText);
        Assert.Equal(1, dialog.ShowMessageCount);
        Assert.Equal(0, dialog.ShowConfirmationCount);
        Assert.Equal("Cloud transcription failed", dialog.LastTitle);
        Assert.Contains("quota exceeded", dialog.LastMessage);
    }

    [AvaloniaFact]
    public async Task TranscriptionFailed_KeyRejected_OffersOpenSettings()
    {
        var pipeline = new MockAudioPipeline();
        var wm = new MockWindowManager();
        var dialog = new MockUserDialogService { ConfirmationResult = true };
        var vm = new TranscribeViewModel(wm, pipeline, dialogService: dialog);
        await vm.StartRecordingAsync();

        pipeline.RaiseTranscriptionFailed(new CloudSpeechTranscriptionException(
            CloudSpeechErrorKind.KeyRejected, "xAI Grok", "xAI Grok rejected the API key (HTTP 401)."));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal("Cloud API key rejected — check Settings", vm.StatusText);
        Assert.Equal(1, dialog.ShowConfirmationCount);
        Assert.Equal(0, dialog.ShowMessageCount);
        Assert.Equal(1, wm.ShowSettingsCount);
        Assert.Equal(SettingsSection.CloudProviders, wm.LastSettingsSection);
    }

    [AvaloniaFact]
    public async Task TranscriptionFailed_NonCloudError_StaysSilent()
    {
        var pipeline = new MockAudioPipeline();
        var dialog = new MockUserDialogService();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, dialogService: dialog);
        await vm.StartRecordingAsync();

        pipeline.RaiseTranscriptionFailed(new InvalidOperationException("local whisper hiccup"));
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, dialog.ShowMessageCount);
        Assert.Equal(0, dialog.ShowConfirmationCount);
        Assert.Equal("Recording...", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task TranscriptionFailed_WhileDialogOpen_DoesNotStackDialogs()
    {
        var pipeline = new MockAudioPipeline();
        var dialog = new MockUserDialogService { Gate = new TaskCompletionSource() };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, dialogService: dialog);
        await vm.StartRecordingAsync();

        var error = new CloudSpeechTranscriptionException(
            CloudSpeechErrorKind.RateLimited, "OpenAI-compatible provider", "rate limit reached");
        pipeline.RaiseTranscriptionFailed(error);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        pipeline.RaiseTranscriptionFailed(error); // arrives while the first dialog is still up
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(1, dialog.ShowMessageCount);

        dialog.Gate.SetResult(); // dismiss; a later failure may show a new dialog
        await Task.Delay(50, TestContext.Current.CancellationToken);
        pipeline.RaiseTranscriptionFailed(error);
        await Task.Delay(50, TestContext.Current.CancellationToken);

        Assert.Equal(2, dialog.ShowMessageCount);
    }

    [AvaloniaFact]
    public async Task StartRecording_GenericFailure_DoesNotShowDialog()
    {
        var pipeline = new MockAudioPipeline
        {
            ThrowOnStart = new InvalidOperationException("mic unavailable")
        };
        var dialog = new MockUserDialogService();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline, dialogService: dialog);

        await vm.StartRecordingAsync();
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal(0, dialog.ShowConfirmationCount);
        Assert.Equal("Ready", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task StopRecording_DuringSlowStart_StillStops()
    {
        // Regression: in Push-to-Talk the very first key release lands while the
        // cold model is still loading. The stop must wait for the in-flight start
        // and then actually stop, instead of no-oping on IsRecording == false and
        // leaving the recording stuck on.
        var pipeline = new MockAudioPipeline
        {
            StartDelay = TimeSpan.FromMilliseconds(300)
        };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        var startTask = vm.StartRecordingAsync();
        await Task.Delay(50); // release arrives mid-load
        await vm.StopRecordingAsync();
        await startTask;

        Assert.False(vm.IsRecording);
        Assert.Equal(1, pipeline.StartCount);
        Assert.Equal(1, pipeline.StopCount);
        Assert.False(pipeline.IsRunning);
        Assert.Equal(RecordingState.Disabled, vm.RecordingState);
        Assert.Equal("Ready", vm.StatusText);
    }

    [AvaloniaFact]
    public async Task StopRecording_DuringFailedStart_DoesNotStopPipeline()
    {
        var pipeline = new MockAudioPipeline
        {
            ThrowOnStart = new InvalidOperationException("mic unavailable")
        };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        var startTask = vm.StartRecordingAsync();
        await vm.StopRecordingAsync();
        await startTask;

        Assert.False(vm.IsRecording);
        Assert.Equal(0, pipeline.StopCount);
    }

    [AvaloniaFact]
    public async Task StartRecording_Reentrant_StartsPipelineOnce()
    {
        var pipeline = new MockAudioPipeline
        {
            StartDelay = TimeSpan.FromMilliseconds(200)
        };
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);

        var first = vm.StartRecordingAsync();
        var second = vm.StartRecordingAsync();
        await Task.WhenAll(first, second);

        Assert.True(vm.IsRecording);
        Assert.Equal(1, pipeline.StartCount);
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

    private sealed class DelayedSpeechEngineSettingsService(string engine) : ISettingsService
    {
        private readonly TaskCompletionSource<bool> _readStarted =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _readCompletion =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task WaitForReadAsync() => _readStarted.Task;

        public void CompleteRead() => _readCompletion.TrySetResult(true);

        public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (key != SettingsKeys.SpeechEngine)
                return default;

            _readStarted.TrySetResult(true);
            await _readCompletion.Task.WaitAsync(cancellationToken);
            return (T?)(object?)engine;
        }

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
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
    public async Task Strip_HiddenForParakeet_ReappearsOnEngineSwitch()
    {
        // Parakeet offers no language choice (auto-detect only, no translation)
        // so the strip hides entirely; switching to Whisper brings it back.
        var (vm, relationship, _, _) = await CreateAsync(SpeechEngine.Parakeet);

        Assert.False(vm.HasLanguageStrip);

        relationship.SetEngine(SpeechEngine.Whisper);
        Assert.True(vm.HasLanguageStrip);
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
    public async Task Strip_LoadsPersistedLanguages_OnConstruction()
    {
        // Regression: the strip must reflect persisted state at startup, not
        // defaults ("Auto = Auto"). The VM — not a pre-call — must trigger the
        // shared relationship's first load.
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), ct);
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, "ru", ct);
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "fr", ct);
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString(), ct);

        var relationship = new LanguageRelationshipViewModel(
            settings, new MockKeyboardLayoutService());
        // Deliberately NOT pre-initialized — the TranscribeViewModel owns it.
        var vm = new TranscribeViewModel(new MockWindowManager(), relationship: relationship);

        await vm.RelationshipInitialization;

        Assert.Equal("Russian", vm.SourceShort);
        Assert.Equal("French", vm.TargetShort);
        Assert.True(relationship.IsConnectorOn);
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
        Assert.Equal(SettingsSection.Language, wm.LastSettingsSection);
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

    [AvaloniaFact]
    public async Task ModelWithoutTranslation_PausesTheStrip()
    {
        var (vm, relationship, _, _) = await CreateAsync();
        relationship.SelectSource("ru");
        relationship.ToggleTranslation();
        Assert.Equal("English", vm.TargetShort);

        relationship.SetWhisperModel(WhisperModelType.LargeV3Turbo);

        // ADR-061: the always-visible strip states what will actually be typed.
        Assert.True(relationship.IsConnectorPaused);
        Assert.Equal("=", relationship.ConnectorGlyph);
        Assert.Equal(vm.SourceShort, vm.TargetShort);
    }

    [AvaloniaFact]
    public async Task PausedNote_RoutesToTheModelPage_AndClosesTheFlyout()
    {
        var (vm, _, _, wm) = await CreateAsync();
        vm.OpenLanguageFlyoutCommand.Execute(null);

        vm.GoToModelSettingsCommand.Execute(null);

        Assert.False(vm.IsLanguageFlyoutOpen);
        Assert.Equal(1, wm.ShowSettingsCount);
        Assert.Equal(SettingsSection.EngineModel, wm.LastSettingsSection);
    }
}
