using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Drives the Transcribe window: a Play (toggle record) button and a
/// Settings button that opens the Settings window via <see cref="IWindowManager"/>.
/// Owns audio-pipeline + text-injection wiring lifted from V1's MainWindowViewModel.
/// </summary>
public partial class TranscribeViewModel : ViewModelBase
{
    private readonly IAudioPipeline? _pipeline;
    private readonly ITextInjectionService? _textInjectionService;
    private readonly IAudioLevelProvider? _audioLevelProvider;
    private readonly IWindowManager _windowManager;
    private readonly LanguageRelationshipViewModel? _relationship;
    private readonly ISettingsService? _settings;
    private readonly IUserDialogService? _dialogService;
    private readonly ILogger<TranscribeViewModel> _logger;
    private readonly object _activeEngineLock = new();
    private bool _hasLiveActiveEngine;

    /// <summary>RMS threshold above which we consider speech active for visual feedback.</summary>
    private const float SpeechThreshold = 0.005f;

    /// <summary>How long Active state persists after speech drops below threshold.</summary>
    private static readonly TimeSpan SpeechHoldOff = TimeSpan.FromMilliseconds(1200);

    /// <summary>Timestamp of the last above-threshold audio level sample.</summary>
    private DateTime _lastSpeechTime;

    /// <summary>
    /// In-flight <see cref="StartRecordingAsync"/>, if any. Lets a stop request
    /// that arrives during the (possibly long) model load wait for the start to
    /// finish instead of being dropped because IsRecording is still false.
    /// All access happens on the UI thread.
    /// </summary>
    private Task? _startTask;

    /// <summary>
    /// Set when a cancel arrives while <see cref="_startTask"/> is still in
    /// flight — a model load cannot be interrupted, so the start path checks
    /// this on completion and tears the recording down instead of entering it.
    /// All access happens on the UI thread.
    /// </summary>
    private bool _cancelRequested;

    /// <summary>Smoothed RMS level for stable state transitions (EMA).</summary>
    private float _smoothedRms;

    /// <summary>EMA factor for rising level (fast attack).</summary>
    private const float RmsAttack = 0.4f;

    /// <summary>EMA factor for falling level (slow decay).</summary>
    private const float RmsDecay = 0.05f;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private RecordingState _recordingState = RecordingState.Disabled;

    [ObservableProperty]
    private float _audioLevel;

    /// <summary>True when recording is active but no speech detected (for button styling).</summary>
    [ObservableProperty]
    private bool _isIdle;

    /// <summary>True when speech is actively detected (for button styling).</summary>
    [ObservableProperty]
    private bool _isActive;

    /// <summary>True while the speech model is loading (for button styling/spinner).</summary>
    [ObservableProperty]
    private bool _isLoading;

    /// <summary>
    /// Reminder of the current dictation gesture, shown as the record button's
    /// tooltip. Pushed in by <see cref="Services.HotkeyCoordinator"/> rather than
    /// read from the hotkey service here — this VM exists in design mode and in
    /// tests where no global hook is running.
    /// </summary>
    [ObservableProperty]
    private string _hotkeyHintText = string.Empty;

    /// <summary>Updates the record button's hotkey reminder. Called on the UI thread.</summary>
    public void SetHotkeyHint(string hint) => HotkeyHintText = hint;

    /// <summary>
    /// The currently active speech engine. Loaded from settings during this
    /// VM's own initialization (the Transcribe window exists before the
    /// Settings window is ever opened, so it cannot rely on anyone else for
    /// its startup state), then kept in sync by
    /// <see cref="Settings.SpeechEngineSettingsViewModel"/> calling
    /// <see cref="SetActiveEngine"/> directly on every live engine switch (the
    /// same pattern it already uses to stop recording / unload the recognizer
    /// on an engine change). Drives the cloud-active indicator (ADR-032
    /// commitment #3) in the Transcribe window.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCloudEngineActive))]
    [NotifyPropertyChangedFor(nameof(CloudProviderLabel))]
    private SpeechEngine _activeEngine = SpeechEngine.Parakeet;

    /// <summary>True when a bring-your-own-key cloud engine is active — audio leaves this machine.</summary>
    public bool IsCloudEngineActive =>
        ActiveEngine is SpeechEngine.OpenAiCompatible or SpeechEngine.XaiGrok;

    /// <summary>Persistent transparency badge text naming the active cloud provider (ADR-032), or null when local.</summary>
    public string? CloudProviderLabel => ActiveEngine switch
    {
        SpeechEngine.OpenAiCompatible => "Cloud: OpenAI-compatible",
        SpeechEngine.XaiGrok => "Cloud: xAI Grok",
        _ => null,
    };

    /// <summary>
    /// Sets the active engine for the cloud-indicator badge. Called by
    /// <see cref="Settings.SpeechEngineSettingsViewModel"/> — not injected the
    /// other way around, since that VM already depends on this one and a
    /// reverse constructor dependency would be circular.
    /// </summary>
    public void SetActiveEngine(SpeechEngine engine)
    {
        lock (_activeEngineLock)
        {
            _hasLiveActiveEngine = true;
            ActiveEngine = engine;
        }
    }

    /// <summary>
    /// Reads the persisted engine so the cloud badge is correct from the very
    /// first frame — without requiring the Settings window (and thus
    /// <see cref="Settings.SpeechEngineSettingsViewModel"/>) to have ever been
    /// constructed. Same parse-with-Parakeet-fallback used across the codebase.
    /// </summary>
    private async Task InitializeActiveEngineAsync()
    {
        if (_settings is null)
            return;

        // Direct property set (no dispatcher hop) — the same pattern every other
        // VM's fire-and-forget InitializeAsync uses for [ObservableProperty] fields.
        var saved = await _settings.GetAsync<string>(SettingsKeys.SpeechEngine);
        var savedEngine = Enum.TryParse<SpeechEngine>(saved, ignoreCase: true, out var parsed)
            ? parsed
            : SpeechEngine.Parakeet;

        lock (_activeEngineLock)
        {
            if (_hasLiveActiveEngine)
                return;

            ActiveEngine = savedEngine;
        }
    }

    partial void OnRecordingStateChanged(RecordingState value)
    {
        IsLoading = value == RecordingState.Loading;
        IsIdle = value == RecordingState.Idle;
        IsActive = value == RecordingState.Active;
    }

    /// <summary>
    /// How long a model load may run before the loading spinner is shown. A hot
    /// (already-loaded) model starts almost instantly, so deferring the spinner by
    /// this much avoids a one-frame icon flicker; only genuine cold loads cross it.
    /// Settable so tests can tune the threshold.
    /// </summary>
    public TimeSpan LoadingSpinnerDelay { get; set; } = TimeSpan.FromMilliseconds(200);

    public TranscribeViewModel(
        IWindowManager windowManager,
        IAudioPipeline? pipeline = null,
        ITextInjectionService? textInjectionService = null,
        IAudioLevelProvider? audioLevelProvider = null,
        LanguageRelationshipViewModel? relationship = null,
        ISettingsService? settings = null,
        IUserDialogService? dialogService = null,
        ILogger<TranscribeViewModel>? logger = null)
    {
        _windowManager = windowManager;
        _pipeline = pipeline;
        _textInjectionService = textInjectionService;
        _audioLevelProvider = audioLevelProvider;
        _relationship = relationship;
        _settings = settings;
        _dialogService = dialogService;
        _logger = logger ?? NullLogger<TranscribeViewModel>.Instance;

        // Self-sufficient badge state at startup: the Transcribe window is
        // created before (and independently of) the Settings window, so waiting
        // for SpeechEngineSettingsViewModel's push would leave a cloud engine
        // undisclosed until the user happens to open Settings — violating
        // ADR-032 commitment #3. Fire-and-forget like other VM initializers;
        // faults are logged, and the Parakeet default stands until the load lands.
        _ = InitializeActiveEngineAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Active engine initialization failed"),
            TaskContinuationOptions.OnlyOnFaulted);

        if (_relationship is not null)
        {
            TargetPicker = new LanguagePickerViewModel(
                header: "Translate to",
                getSupported: () => _relationship.TargetLanguages,
                getRecents: () => _relationship.TargetRecent,
                getSelectedCode: () => _relationship.TranslationEnabled
                    ? _relationship.TargetCode
                    : LanguageCatalog.NoTranslationCode,
                onSelect: SelectTargetFromFlyout,
                getSpecials: () =>
                    [new LanguageSpecialRow(
                        LanguageCatalog.NoTranslationCode, "Off — no translation",
                        SubHint: null, LanguageRowIcon.Off)]);

            _relationship.PropertyChanged += OnRelationshipPropertyChanged;
            // Recording must restart to pick up new languages, whichever surface
            // changed them (this window's flyout or the Settings page).
            _relationship.RelationshipChanged += (_, _) => _ = StopRecordingAsync();

            // Load persisted languages so the strip reflects real state at
            // startup, not defaults. Idempotent with the Settings page's call —
            // whichever surface is built first performs the one load.
            RelationshipInitialization = _relationship.InitializeAsync();
            RelationshipInitialization.ContinueWith(
                t => _logger.LogError(t.Exception, "Language relationship initialization failed"),
                TaskContinuationOptions.OnlyOnFaulted);
        }
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public TranscribeViewModel() : this(new DesignWindowManager()) { }

    // ----- Language quick-picker strip (spec §2.2, FR-W1..W5) -----------------

    /// <summary>The shared relationship; null in designer/legacy-test contexts.</summary>
    public LanguageRelationshipViewModel? Relationship => _relationship;

    /// <summary>
    /// Strip + flyout render only when the relationship is wired and the active
    /// engine actually offers a language choice — Parakeet (auto-detect only,
    /// no translation) hides the strip so the widget stays compact.
    /// </summary>
    public bool HasLanguageStrip => _relationship is { HasLanguageChoices: true };

    /// <summary>
    /// The persisted-state load kicked off in the constructor. Exposed so hosts
    /// and tests can await first-load completion; <see cref="Task.CompletedTask"/>
    /// when no relationship is wired.
    /// </summary>
    public Task RelationshipInitialization { get; } = Task.CompletedTask;

    /// <summary>Target picker embedded in the flyout (full form only).</summary>
    public LanguagePickerViewModel? TargetPicker { get; }

    [ObservableProperty]
    private bool _isLanguageFlyoutOpen;

    /// <summary>Compact source chip: detected/explicit language name or "Auto".</summary>
    public string SourceShort
    {
        get
        {
            if (_relationship is null)
                return "";
            var code = _relationship.SourceCode;
            if (LanguageCatalog.IsKeyboardLayout(code))
            {
                return _relationship.DetectedKeyboardLayout is { } layout
                    ? LanguageCatalog.GetEnglishName(layout.LanguageCode)
                    : "Keyboard";
            }
            return LanguageCatalog.IsAutoDetect(code) ? "Auto" : LanguageCatalog.GetEnglishName(code);
        }
    }

    /// <summary>
    /// Compact target chip: the target name while translating, otherwise the
    /// source mirrored — "English = English" reads "typed as spoken".
    /// </summary>
    public string TargetShort =>
        _relationship is null ? ""
        : _relationship.TranslationEnabled && !_relationship.IsNoneForm
            ? LanguageCatalog.GetEnglishName(_relationship.TargetCode)
            : SourceShort;

    [RelayCommand]
    private void ToggleTranslation() => _relationship?.ToggleTranslation();

    [RelayCommand]
    private void OpenLanguageFlyout()
    {
        if (_relationship is null)
            return;

        var willOpen = !IsLanguageFlyoutOpen;
        if (willOpen)
        {
            _relationship.RefreshKeyboardLayout();
            if (TargetPicker is not null)
            {
                TargetPicker.Filter = "";
                TargetPicker.Refresh();
            }
        }

        IsLanguageFlyoutOpen = willOpen;
    }

    /// <summary>Source editing lives in Settings — the flyout row routes there.</summary>
    [RelayCommand]
    private void GoToLanguageSettings()
    {
        IsLanguageFlyoutOpen = false;
        _windowManager.ShowSettings(SettingsSection.Language);
    }

    private void SelectTargetFromFlyout(string code)
    {
        _relationship?.SelectTarget(code);
        IsLanguageFlyoutOpen = false;
    }

    private void OnRelationshipPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LanguageRelationshipViewModel.SourceCode):
            case nameof(LanguageRelationshipViewModel.DetectedKeyboardLayout):
            case nameof(LanguageRelationshipViewModel.TranslationEnabled):
            case nameof(LanguageRelationshipViewModel.TargetCode):
                OnPropertyChanged(nameof(SourceShort));
                OnPropertyChanged(nameof(TargetShort));
                break;

            case nameof(LanguageRelationshipViewModel.Capabilities):
                OnPropertyChanged(nameof(SourceShort));
                OnPropertyChanged(nameof(TargetShort));
                // Engine switches can add/remove the strip (e.g. Parakeet has
                // no language choices) — the window resizes on this change.
                OnPropertyChanged(nameof(HasLanguageStrip));
                break;
        }
    }

    [RelayCommand]
    private async Task TogglePlayAsync()
    {
        if (IsRecording)
            await StopRecordingAsync();
        else
            await StartRecordingAsync();
    }

    [RelayCommand]
    private void OpenSettings() => _windowManager.ShowSettings();

    /// <summary>
    /// Loads the speech model in the background so the first record press is
    /// instant. Best-effort and silent — failures are logged, not surfaced, and
    /// a cold load still falls back to the on-button loading spinner. Safe to call
    /// repeatedly; the pipeline de-duplicates the heavy load.
    /// </summary>
    public async Task PrewarmAsync()
    {
        if (_pipeline is null || IsRecording)
            return;

        try
        {
            await _pipeline.PrewarmAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Speech model prewarm failed; first record press will load on demand");
        }
    }

    public async Task StartRecordingAsync()
    {
        if (_pipeline is null || IsRecording || _startTask is not null)
            return;

        _cancelRequested = false;
        var startTask = StartRecordingCoreAsync(_pipeline);
        _startTask = startTask;
        try
        {
            await startTask;
        }
        finally
        {
            _startTask = null;
            _cancelRequested = false;
        }
    }

    private async Task StartRecordingCoreAsync(IAudioPipeline pipeline)
    {
        try
        {
            pipeline.TranscriptionAvailable += OnTranscriptionAvailable;
            pipeline.TranscriptionFailed += OnTranscriptionFailed;
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged += OnAudioLevelChanged;

            var startTask = pipeline.StartAsync(PipelineMode.Batch);

            // Defer the spinner: a hot model starts almost instantly, so only show
            // the loading state when the load actually outlasts the threshold —
            // otherwise the icon would flash for a single frame. A cancel that
            // already landed skips it entirely; the load runs on regardless, but
            // showing a spinner for work the user has abandoned would strand the
            // widget on "Loading model..." after they pressed Escape.
            if (await Task.WhenAny(startTask, Task.Delay(LoadingSpinnerDelay)) != startTask
                && !_cancelRequested)
            {
                RecordingState = RecordingState.Loading;
                StatusText = "Loading model...";
            }

            await startTask;

            // Escape arrived while the model was still loading. The pipeline is
            // running now, so it still has to be shut down — but the UI was
            // already returned to a cancelled state and must not flash into
            // "Recording..." on its way back out.
            if (_cancelRequested)
            {
                await DiscardStartedRecordingAsync(pipeline);
                return;
            }

            IsRecording = true;
            RecordingState = RecordingState.Idle;
            StatusText = "Recording...";
        }
        catch (RuntimeUnavailableException ex)
        {
            _logger.LogWarning(ex, "Whisper runtime '{Runtime}' unavailable", ex.Requested);
            pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            pipeline.TranscriptionFailed -= OnTranscriptionFailed;
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged -= OnAudioLevelChanged;
            IsRecording = false;
            RecordingState = RecordingState.Disabled;
            StatusText = $"{ex.Requested} runtime not available — change in Settings";
        }
        catch (CloudProviderNotConfiguredException ex)
        {
            // Must precede the generic catch — this derives from
            // InvalidOperationException. A cloud engine without an API key is a
            // user-fixable state, so besides resetting like the generic path we
            // route the user to the Cloud providers settings via a dialog.
            _logger.LogWarning(ex, "Cloud engine {Engine} is not configured", ex.Engine);
            pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            pipeline.TranscriptionFailed -= OnTranscriptionFailed;
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged -= OnAudioLevelChanged;
            IsRecording = false;
            RecordingState = RecordingState.Disabled;
            StatusText = "Cloud provider not configured";

            // Fire-and-forget on purpose: StopRecordingAsync awaits _startTask
            // (ADR-039), so awaiting a modal dialog here would make a
            // push-to-talk key release hang until the dialog is dismissed.
            _ = ShowCloudProviderConfigDialogAsync(ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            pipeline.TranscriptionFailed -= OnTranscriptionFailed;
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged -= OnAudioLevelChanged;
            IsRecording = false;
            RecordingState = RecordingState.Disabled;
            StatusText = "Ready";
        }
    }

    /// <summary>
    /// Tells the user why recording could not start (cloud engine selected but
    /// no API key stored) and, on confirmation, opens the Settings window on
    /// the Cloud providers section. No-op without a dialog service (design
    /// mode / tests that don't care about the dialog).
    /// </summary>
    private async Task ShowCloudProviderConfigDialogAsync(CloudProviderNotConfiguredException ex)
    {
        if (_dialogService is null)
            return;

        try
        {
            var openSettings = await _dialogService.ShowConfirmationAsync(
                "Cloud provider not configured",
                ex.Message,
                confirmText: "Open settings",
                cancelText: "Cancel");

            if (openSettings)
                _windowManager.ShowSettings(SettingsSection.CloudProviders);
        }
        catch (Exception dialogEx)
        {
            _logger.LogError(dialogEx, "Failed to show cloud-provider configuration dialog");
        }
    }

    public async Task StopRecordingAsync()
    {
        if (_pipeline is null)
            return;

        // In Push-to-Talk the key release can arrive while the initial model
        // load is still running and IsRecording is not yet true. Wait for the
        // in-flight start to settle so the stop isn't silently dropped.
        if (_startTask is { } startTask)
            await startTask;

        if (!IsRecording)
            return;

        try
        {
            await _pipeline.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop recording");
        }
        finally
        {
            DetachPipelineHandlers();
            ResetRecordingState();
        }
    }

    /// <summary>
    /// Stops recording and throws the audio away — nothing is transcribed and
    /// nothing is typed. Detaching the handlers before stopping the pipeline is
    /// what makes it a discard: whatever the pipeline still produces has nowhere
    /// left to go.
    /// </summary>
    public async Task CancelRecordingAsync()
    {
        if (_pipeline is null)
            return;

        // Unlike StopRecordingAsync, a cancel must *not* wait on an in-flight
        // start. A stop waits because the user wants that recording (ADR-039);
        // someone pressing Escape wants out now, and a cold model load can run
        // for seconds. So the UI is released immediately and the intent is
        // handed to the start path, which discards the recording the moment the
        // load it cannot interrupt finally completes.
        if (_startTask is not null)
        {
            _cancelRequested = true;
            DetachPipelineHandlers();
            ResetRecordingState();
            StatusText = "Cancelled";
            return;
        }

        if (!IsRecording)
            return;

        DetachPipelineHandlers();

        try
        {
            await _pipeline.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel recording");
        }
        finally
        {
            ResetRecordingState();
            StatusText = "Cancelled";
        }
    }

    /// <summary>
    /// Shuts down a recording that only came into being because the model
    /// finished loading after the user had already cancelled. The UI state is
    /// left alone — <see cref="CancelRecordingAsync"/> already set it.
    /// </summary>
    private async Task DiscardStartedRecordingAsync(IAudioPipeline pipeline)
    {
        DetachPipelineHandlers();

        try
        {
            await pipeline.StopAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discard a recording cancelled during model load");
        }
    }

    private void DetachPipelineHandlers()
    {
        if (_pipeline is not null)
        {
            _pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            _pipeline.TranscriptionFailed -= OnTranscriptionFailed;
        }

        if (_audioLevelProvider is not null)
            _audioLevelProvider.LevelChanged -= OnAudioLevelChanged;
    }

    private void ResetRecordingState()
    {
        IsRecording = false;
        RecordingState = RecordingState.Disabled;
        AudioLevel = 0f;
        _smoothedRms = 0f;
        StatusText = "Ready";
    }

    private async void OnTranscriptionAvailable(object? sender, TranscriptionEventArgs e)
    {
        if (_textInjectionService is null || string.IsNullOrWhiteSpace(e.Result.Text))
            return;

        try
        {
            await _textInjectionService.InjectTextAsync(e.Result.Text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject transcribed text");
        }
    }

    /// <summary>
    /// Guards against stacking one error dialog per failed utterance — while a
    /// dialog is up, further failures only update <see cref="StatusText"/>.
    /// Touched exclusively on the UI thread.
    /// </summary>
    private bool _isCloudErrorDialogOpen;

    /// <summary>
    /// Surfaces cloud transcription failures (quota exhausted, rate limit,
    /// rejected key, provider outage — ADR-043 amendment). The pipeline keeps
    /// recording, so this must not stop anything; it informs. Raised from the
    /// pipeline's background processing task, hence the dispatcher hop.
    /// </summary>
    private void OnTranscriptionFailed(object? sender, TranscriptionErrorEventArgs e)
    {
        if (e.Exception is not CloudSpeechTranscriptionException cloudEx)
            return; // local-engine failures keep their existing log-only behaviour

        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            StatusText = cloudEx.Kind switch
            {
                CloudSpeechErrorKind.KeyRejected => "Cloud API key rejected — check Settings",
                CloudSpeechErrorKind.QuotaExceeded => "Cloud quota exceeded — check plan & billing",
                CloudSpeechErrorKind.RateLimited => "Cloud rate limit reached — try again shortly",
                CloudSpeechErrorKind.ProviderUnavailable => "Cloud provider unavailable — try again shortly",
                _ => "Cloud transcription failed",
            };

            if (_isCloudErrorDialogOpen)
                return;

            _isCloudErrorDialogOpen = true;
            _ = ShowCloudTranscriptionErrorDialogAsync(cloudEx);
        });
    }

    /// <summary>
    /// Fire-and-forget dialog for a failed cloud transcription. A rejected key
    /// is user-fixable in Settings, so that kind gets an "Open settings"
    /// action (deep link to Cloud providers); everything else is informational.
    /// </summary>
    private async Task ShowCloudTranscriptionErrorDialogAsync(CloudSpeechTranscriptionException ex)
    {
        try
        {
            if (_dialogService is null)
                return;

            if (ex.Kind == CloudSpeechErrorKind.KeyRejected)
            {
                var openSettings = await _dialogService.ShowConfirmationAsync(
                    "Cloud transcription failed",
                    ex.Message,
                    confirmText: "Open settings",
                    cancelText: "Cancel");

                if (openSettings)
                    _windowManager.ShowSettings(SettingsSection.CloudProviders);
            }
            else
            {
                await _dialogService.ShowMessageAsync("Cloud transcription failed", ex.Message, "OK");
            }
        }
        catch (Exception dialogEx)
        {
            _logger.LogError(dialogEx, "Failed to show cloud transcription error dialog");
        }
        finally
        {
            _isCloudErrorDialogOpen = false;
        }
    }

    private void OnAudioLevelChanged(object? sender, AudioLevelEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            AudioLevel = e.Level;

            // Exponential moving average: fast attack, slow decay
            var factor = e.Level > _smoothedRms ? RmsAttack : RmsDecay;
            _smoothedRms += (e.Level - _smoothedRms) * factor;

            if (_smoothedRms >= SpeechThreshold)
            {
                _lastSpeechTime = DateTime.UtcNow;
                RecordingState = RecordingState.Active;
            }
            else if (RecordingState == RecordingState.Active)
            {
                // Hold Active state for SpeechHoldOff after last speech
                if (DateTime.UtcNow - _lastSpeechTime >= SpeechHoldOff)
                    RecordingState = RecordingState.Idle;
            }
        });
    }

    private sealed class DesignWindowManager : IWindowManager
    {
        public void ShowTranscribe(bool activate = true) { }
        public void ShowSettings(SettingsSection? section = null) { }
        public void HideTranscribe() { }
        public void Exit() { }
    }
}
