using System.Diagnostics;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Drives the Transcribe window: a Play (toggle record) button and a
/// Settings button that opens the Settings window via <see cref="IWindowManager"/>.
/// Owns audio-pipeline + text-injection wiring lifted from V1's MainWindowViewModel.
/// </summary>
public partial class TranscribeViewModel : ViewModelBase
{
    /// <summary>
    /// What <see cref="StatusText"/> currently says, independent of language —
    /// tracked so an interface-language switch can recompute the visible text
    /// instead of leaving whatever was rendered in the previous language
    /// (ADR-064 amendment). <see cref="StatusText"/> itself stays a plain
    /// stored string because most of the app only ever needs to display it,
    /// never re-derive it.
    /// </summary>
    private enum StatusKind
    {
        Ready,
        LoadingModel,
        Recording,
        Cancelled,
        RuntimeRestartRequired,
        RuntimeUnavailable,
        CloudNotConfigured,
        CloudKeyRejected,
        CloudQuotaExceeded,
        CloudRateLimited,
        CloudProviderUnavailable,
        CloudFailed,
    }

    private StatusKind _statusKind = StatusKind.Ready;

    /// <summary>The runtime name substituted into the two Runtime* kinds; unused otherwise.</summary>
    private object? _statusParam;

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
    private static readonly TimeSpan SpeechHoldOff = TimeSpan.FromMilliseconds(180);

    /// <summary>Timestamp of the last above-threshold audio level sample.</summary>
    private long _lastSpeechTime;
    private long _recordingStartedAt;
    private DispatcherTimer? _speechSilenceTimer;

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

    [ObservableProperty]
    private string _statusText = Strings.Transcribe_Status_Ready;

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
        SpeechEngine.OpenAiCompatible => Strings.Transcribe_CloudProvider_OpenAiCompatible,
        SpeechEngine.XaiGrok => Strings.Transcribe_CloudProvider_XaiGrok,
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

    /// <summary>
    /// Sets <see cref="StatusText"/> and remembers how to recompute it, so
    /// <see cref="OnCultureChanged"/> can re-render it in the new language
    /// instead of leaving stale text on screen.
    /// </summary>
    private void SetStatus(StatusKind kind, object? param = null)
    {
        _statusKind = kind;
        _statusParam = param;
        StatusText = ComputeStatusText(kind, param);
    }

    private static string ComputeStatusText(StatusKind kind, object? param) => kind switch
    {
        StatusKind.LoadingModel => Strings.Transcribe_Status_LoadingModel,
        StatusKind.Recording => Strings.Transcribe_Status_Recording,
        StatusKind.Cancelled => Strings.Transcribe_Status_Cancelled,
        StatusKind.RuntimeRestartRequired => Strings.Format_Transcribe_Status_RuntimeRestartRequiredFormat(param),
        StatusKind.RuntimeUnavailable => Strings.Format_Transcribe_Status_RuntimeUnavailableFormat(param),
        StatusKind.CloudNotConfigured => Strings.Transcribe_Status_CloudNotConfigured,
        StatusKind.CloudKeyRejected => Strings.Transcribe_Status_CloudKeyRejected,
        StatusKind.CloudQuotaExceeded => Strings.Transcribe_Status_CloudQuotaExceeded,
        StatusKind.CloudRateLimited => Strings.Transcribe_Status_CloudRateLimited,
        StatusKind.CloudProviderUnavailable => Strings.Transcribe_Status_CloudProviderUnavailable,
        StatusKind.CloudFailed => Strings.Transcribe_Status_CloudFailed,
        _ => Strings.Transcribe_Status_Ready,
    };

    /// <summary>
    /// Re-renders everything this VM composes in C# after an interface-language
    /// switch (ADR-064 amendment). Text reaching the screen through
    /// <c>{loc:Tr}</c> updates itself; this is for the rest — see the
    /// localization skill.
    /// </summary>
    private void OnCultureChanged()
    {
        StatusText = ComputeStatusText(_statusKind, _statusParam);
        OnPropertyChanged(nameof(CloudProviderLabel));
        OnPropertyChanged(nameof(SourceShort));
        OnPropertyChanged(nameof(TargetShort));
        TargetPicker?.Refresh();
    }

    partial void OnRecordingStateChanged(RecordingState value)
    {
        IsLoading = value == RecordingState.Loading;
        IsIdle = value == RecordingState.Idle;
        IsActive = value == RecordingState.Active;
        if (value != RecordingState.Active)
            StopSpeechSilenceTimer();
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

        // This VM is not a settings section, so it has no OnCultureChanged hook
        // to override — subscribe directly, matching TranscribeViewModel's
        // status/badge/chip text being composed in C# rather than {loc:Tr}
        // (ADR-064 amendment).
        Localizer.Instance.CultureChanged += (_, _) => OnCultureChanged();

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
                getHeader: () => Strings.Transcribe_TargetPicker_Header,
                getSupported: () => _relationship.TargetLanguages,
                getRecents: () => _relationship.TargetRecent,
                getSelectedCode: () => _relationship.TranslationEnabled
                    ? _relationship.TargetCode
                    : LanguageCatalog.NoTranslationCode,
                onSelect: SelectTargetFromFlyout,
                getSpecials: () =>
                    [new LanguageSpecialRow(
                        LanguageCatalog.NoTranslationCode, Strings.Transcribe_TargetPicker_Off,
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
                    : Strings.Transcribe_Source_Keyboard;
            }
            return LanguageCatalog.IsAutoDetect(code) ? Strings.Transcribe_Source_Auto : LanguageCatalog.GetEnglishName(code);
        }
    }

    /// <summary>
    /// Compact target chip: the target name while translating, otherwise the
    /// source mirrored — "English = English" reads "typed as spoken". A paused
    /// translation mirrors too (ADR-061): the strip states what will actually be
    /// typed, and the amber connector says why it isn't the chosen target.
    /// </summary>
    public string TargetShort =>
        _relationship is null ? ""
        : _relationship.TranslationEnabled
          && !_relationship.IsNoneForm
          && !_relationship.IsTranslationPaused
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

    /// <summary>
    /// The paused-translation note's way out (ADR-061): the model page of the
    /// active engine, where a multilingual model resumes translation.
    /// </summary>
    [RelayCommand]
    private void GoToModelSettings()
    {
        IsLanguageFlyoutOpen = false;
        _windowManager.ShowSettings(SettingsSection.EngineModel);
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
            case nameof(LanguageRelationshipViewModel.IsTranslationPaused):
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

    /// <param name="holdScoped">True when the caller's gesture ends the utterance on
    /// key release (a hold, or a push-to-talk chord). Such a session runs as
    /// <see cref="PipelineMode.SingleUtterance"/>: the release is a better
    /// end-of-utterance signal than any silence timeout, and letting silence cut first
    /// splits sentences mid-flow (ADR-060). The widget's record button leaves this
    /// false — it has no release to wait for.</param>
    public async Task StartRecordingAsync(bool holdScoped = false)
    {
        if (_pipeline is null || IsRecording || _startTask is not null)
            return;

        _cancelRequested = false;
        var startTask = StartRecordingCoreAsync(_pipeline, holdScoped);
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

    private async Task StartRecordingCoreAsync(IAudioPipeline pipeline, bool holdScoped)
    {
        try
        {
            pipeline.TranscriptionAvailable += OnTranscriptionAvailable;
            pipeline.TranscriptionFailed += OnTranscriptionFailed;
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged += OnAudioLevelChanged;

            var startTask = pipeline.StartAsync(
                holdScoped ? PipelineMode.SingleUtterance : PipelineMode.Batch);

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
                SetStatus(StatusKind.LoadingModel);
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

            _recordingStartedAt = Stopwatch.GetTimestamp();
            IsRecording = true;
            RecordingState = RecordingState.Idle;
            SetStatus(StatusKind.Recording);
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
            // A latched runtime is not a broken machine — the fix is a restart, not
            // a different setting, so say so instead of sending the user to Settings.
            SetStatus(
                ex.RequiresRestart ? StatusKind.RuntimeRestartRequired : StatusKind.RuntimeUnavailable,
                ex.Requested);
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
            SetStatus(StatusKind.CloudNotConfigured);

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
            SetStatus(StatusKind.Ready);
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
                Strings.Dialog_CloudNotConfigured_Title,
                CloudErrorText.NotConfigured(ex),
                confirmText: Strings.Common_OpenSettings,
                cancelText: Strings.Common_Cancel);

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
    /// nothing is typed. The pipeline's own discard path drops the buffered
    /// audio and cancels any recognizer call in flight; detaching the handlers
    /// first covers the utterance that finished between the keystroke and here.
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
            SetStatus(StatusKind.Cancelled);
            return;
        }

        if (!IsRecording)
            return;

        DetachPipelineHandlers();

        try
        {
            await _pipeline.CancelAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cancel recording");
        }
        finally
        {
            ResetRecordingState();
            SetStatus(StatusKind.Cancelled);
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
            await pipeline.CancelAsync();
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
        _lastSpeechTime = 0;
        SetStatus(StatusKind.Ready);
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
            SetStatus(cloudEx.Kind switch
            {
                CloudSpeechErrorKind.KeyRejected => StatusKind.CloudKeyRejected,
                CloudSpeechErrorKind.QuotaExceeded => StatusKind.CloudQuotaExceeded,
                CloudSpeechErrorKind.RateLimited => StatusKind.CloudRateLimited,
                CloudSpeechErrorKind.ProviderUnavailable => StatusKind.CloudProviderUnavailable,
                _ => StatusKind.CloudFailed,
            });

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
                    Strings.Dialog_CloudTranscriptionFailed_Title,
                    CloudErrorText.Transcription(ex),
                    confirmText: Strings.Common_OpenSettings,
                    cancelText: Strings.Common_Cancel);

                if (openSettings)
                    _windowManager.ShowSettings(SettingsSection.CloudProviders);
            }
            else
            {
                await _dialogService.ShowMessageAsync(
                    Strings.Dialog_CloudTranscriptionFailed_Title,
                    CloudErrorText.Transcription(ex),
                    Strings.Common_Ok);
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
        var receivedAt = Stopwatch.GetTimestamp();
        Dispatcher.UIThread.Post(() =>
        {
            // A callback already queued when recording stopped must not revive the wave.
            if (!IsRecording || receivedAt < _recordingStartedAt || Stopwatch.GetElapsedTime(receivedAt) >= SpeechHoldOff)
                return;

            var level = float.IsFinite(e.Level) ? Math.Clamp(e.Level, 0, 1) : 0;
            var threshold = RecordingState == RecordingState.Active ? 0.0035f : SpeechThreshold;
            AudioLevel = level >= threshold ? level : 0;
            if (level >= threshold)
            {
                // Measure the hold from actual audio, never from a slowly decaying EMA.
                _lastSpeechTime = receivedAt;
                RecordingState = RecordingState.Active;
                if (_speechSilenceTimer is null)
                {
                    _speechSilenceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(40) };
                    _speechSilenceTimer.Tick += OnSpeechSilenceTick;
                    _speechSilenceTimer.Start();
                }
            }
        });
    }

    private void OnSpeechSilenceTick(object? sender, EventArgs e)
    {
        // Also settle when capture stops delivering callbacks during silence.
        if (Stopwatch.GetElapsedTime(_lastSpeechTime) >= SpeechHoldOff)
        {
            AudioLevel = 0;
            RecordingState = RecordingState.Idle;
        }
    }

    private void StopSpeechSilenceTimer()
    {
        if (_speechSilenceTimer is null)
            return;
        _speechSilenceTimer.Stop();
        _speechSilenceTimer.Tick -= OnSpeechSilenceTick;
        _speechSilenceTimer = null;
    }

    private sealed class DesignWindowManager : IWindowManager
    {
        public void ShowTranscribe(bool activate = true) { }
        public void ShowSettings(SettingsSection? section = null) { }
        public void HideTranscribe() { }
        public void Exit() { }
    }
}
