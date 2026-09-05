using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

/// <summary>Visual state of the connector between the source and target cards.</summary>
public enum ConnectorState
{
    /// <summary>Translation on — accent "→".</summary>
    On,

    /// <summary>Translation off — muted "=".</summary>
    Off,

    /// <summary>Engine cannot translate — locked "=" at half opacity.</summary>
    Locked,

    /// <summary>
    /// Translation is switched on but the active Whisper model can't perform it
    /// (ADR-061) — amber "=". Distinct from <see cref="Locked"/>: the engine is
    /// capable, only this model isn't, so the state is reversible by picking a
    /// multilingual model and the user's preference stays on.
    /// </summary>
    Paused,
}

/// <summary>
/// The single source of truth for the source → target language relationship,
/// shared by the Settings → Language page and the Transcribe window quick picker
/// so the two surfaces never drift (spec §7 state machine).
///
/// <para>Owns: the active engine's <see cref="LanguageCapabilities"/>, the source
/// state (keyboard / auto / explicit language), the target state (translation
/// on/off + resting target code), per-role MRU lists, persistence, the derived
/// connector / summary strings, and the fallback-on-engine-switch logic with its
/// explanatory toasts (spec §8).</para>
///
/// <para>The resting <see cref="TargetCode"/> is preserved when translation is
/// toggled off, so re-enabling restores the last-used target without re-asking
/// (spec §9). The keyboard sentinel persists as-is; the pipeline resolves it via
/// <see cref="IKeyboardLayoutService"/> at recording start.</para>
/// </summary>
public sealed partial class LanguageRelationshipViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IKeyboardLayoutService _keyboardLayout;
    private readonly ILogger<LanguageRelationshipViewModel> _logger;

    private List<string> _sourceRecent = [];
    private List<string> _targetRecent = [];
    private CancellationTokenSource? _toastCts;
    private bool _suppressToasts;
    private bool _initialized;

    private DispatcherTimer? _layoutPollTimer;
    private int _livePollSubscribers;

    /// <summary>
    /// How often live polling re-detects the OS keyboard layout while a surface
    /// that displays it is visible. Keyboard-layout changes (e.g. Alt+Shift)
    /// raise no event a background tray app can observe, so the displayed
    /// "Detected: …" hint is kept current by polling instead.
    /// </summary>
    private static readonly TimeSpan LayoutPollInterval = TimeSpan.FromMilliseconds(500);

    /// <summary>Toast lifetime before auto-clear. Shortened in tests.</summary>
    internal TimeSpan ToastDuration { get; set; } = TimeSpan.FromSeconds(4);

    /// <summary>
    /// Raised after any user-visible selection change has been applied and queued
    /// for persistence. Hosts use this to stop an in-flight recording so the next
    /// one picks up the new languages.
    /// </summary>
    public event EventHandler? RelationshipChanged;

    /// <summary>The active engine's language capabilities.</summary>
    public LanguageCapabilities Capabilities { get; private set; } =
        SpeechEngineCapabilities.For(SpeechEngine.Parakeet);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EngineDisplayName))]
    private SpeechEngine _engine = SpeechEngine.Parakeet;

    /// <summary>Short human name of the active engine ("Whisper", "Gemma 4").</summary>
    public string EngineDisplayName => EngineName(Engine);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceDisplayLabel))]
    [NotifyPropertyChangedFor(nameof(SourceSubHint))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string _sourceCode = LanguageCatalog.AutoDetectCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetDisplayLabel))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private string _targetCode = LanguageCatalog.NoTranslationCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Connector))]
    [NotifyPropertyChangedFor(nameof(ConnectorGlyph))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    [NotifyPropertyChangedFor(nameof(IsTranslationPaused))]
    [NotifyPropertyChangedFor(nameof(ConnectorTooltip))]
    [NotifyPropertyChangedFor(nameof(TranslationSwitch))]
    [NotifyPropertyChangedFor(nameof(IsConnectorOn))]
    [NotifyPropertyChangedFor(nameof(IsConnectorOff))]
    [NotifyPropertyChangedFor(nameof(IsConnectorPaused))]
    [NotifyPropertyChangedFor(nameof(TargetDisplayLabel))]
    private bool _translationEnabled;

    /// <summary>
    /// Whether the active Whisper model can translate (ADR-033). Drives the
    /// paused state; only meaningful on the toggle form.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTranslationPaused))]
    [NotifyPropertyChangedFor(nameof(Connector))]
    [NotifyPropertyChangedFor(nameof(ConnectorGlyph))]
    [NotifyPropertyChangedFor(nameof(ConnectorTooltip))]
    [NotifyPropertyChangedFor(nameof(IsConnectorOn))]
    [NotifyPropertyChangedFor(nameof(IsConnectorOff))]
    [NotifyPropertyChangedFor(nameof(IsConnectorPaused))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private bool _whisperModelSupportsTranslation = true;

    /// <summary>
    /// The active Whisper model, kept so the paused note and tooltip can name
    /// the model that caused the pause rather than blaming "the model" vaguely.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(WhisperModelDisplayName))]
    [NotifyPropertyChangedFor(nameof(TranslationPausedNote))]
    [NotifyPropertyChangedFor(nameof(ConnectorTooltip))]
    private WhisperModelType _whisperModel = WhisperModelType.Base;

    /// <summary>Transient one-line fallback explanation (spec §8); auto-clears.</summary>
    [ObservableProperty]
    private string? _toastMessage;

    /// <summary>
    /// The detected OS keyboard layout, refreshed on initialization and via
    /// <see cref="RefreshKeyboardLayout"/>. Null when detection is unavailable.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceSubHint))]
    [NotifyPropertyChangedFor(nameof(SourceDisplayLabel))]
    [NotifyPropertyChangedFor(nameof(SummaryText))]
    private KeyboardLayoutInfo? _detectedKeyboardLayout;

    public LanguageRelationshipViewModel(
        ISettingsService settings,
        IKeyboardLayoutService keyboardLayout,
        ILogger<LanguageRelationshipViewModel>? logger = null)
    {
        _settings = settings;
        _keyboardLayout = keyboardLayout;
        _logger = logger ?? NullLogger<LanguageRelationshipViewModel>.Instance;
    }

    // ----- Derived state -------------------------------------------------

    /// <summary>The form the target control takes for the active engine.</summary>
    public TranslationForm TargetForm => Capabilities.TranslationForm;

    /// <summary>
    /// Whether the active engine offers any language choice. When false
    /// (Parakeet: auto-detect only, no translation) every language surface —
    /// the Settings page and the Transcribe strip — hides entirely.
    /// </summary>
    public bool HasLanguageChoices => Capabilities.HasLanguageChoices;

    public ConnectorState Connector =>
        TargetForm == TranslationForm.None ? ConnectorState.Locked
        : !TranslationEnabled ? ConnectorState.Off
        : IsTranslationPaused ? ConnectorState.Paused
        : ConnectorState.On;

    // Only a live translation earns the arrow. Paused shares the "=" of an off
    // connector because that is what the output will actually be — the amber
    // styling and the tooltip carry the difference.
    public string ConnectorGlyph => Connector == ConnectorState.On ? "→" : "=";

    /// <summary>
    /// Tooltip for the connector control on both surfaces. The paused case is
    /// the only way the compact strip can explain itself without being opened.
    /// </summary>
    public string ConnectorTooltip => Connector switch
    {
        ConnectorState.Locked => UnavailableNote,
        ConnectorState.Paused =>
            $"Translation paused — \"{WhisperModelDisplayName}\" can't translate",
        ConnectorState.On => "Turn translation off",
        _ => "Turn translation on",
    };

    // Boolean projections of TargetForm / Connector for Classes.* and IsVisible
    // bindings, shared by the Settings page and the Transcribe strip/flyout.
    public bool IsToggleForm => TargetForm == TranslationForm.Toggle;
    public bool IsFullForm => TargetForm == TranslationForm.Full;
    public bool IsNoneForm => TargetForm == TranslationForm.None;
    public bool IsConnectorOn => Connector == ConnectorState.On;
    public bool IsConnectorOff => Connector == ConnectorState.Off;
    public bool IsConnectorLocked => Connector == ConnectorState.Locked;
    public bool IsConnectorPaused => Connector == ConnectorState.Paused;

    /// <summary>
    /// Two-way surface for <c>ToggleSwitch</c> bindings. Routed through
    /// <see cref="ToggleTranslation"/> so default-target selection and
    /// persistence apply instead of a bare property write.
    /// </summary>
    public bool TranslationSwitch
    {
        get => TranslationEnabled;
        set
        {
            if (value != TranslationEnabled)
                ToggleTranslation();
        }
    }

    /// <summary>Label for the toggle-form switch (e.g. "Translate to English").</summary>
    public string ToggleSwitchLabel =>
        Capabilities.FixedTranslationTargets.Count > 0
            ? $"Translate to {LanguageCatalog.GetEnglishName(Capabilities.FixedTranslationTargets[0].Code)}"
            : "Translate";

    /// <summary>Amber inline note for the none form, naming the model (spec §4).</summary>
    public string UnavailableNote =>
        $"{EngineDisplayName} can't translate — Parlotype types exactly what you say.";

    /// <summary>Resting label for the source card.</summary>
    public string SourceDisplayLabel =>
        LanguageCatalog.IsKeyboardLayout(SourceCode) ? "System keyboard layout"
        : LanguageCatalog.IsAutoDetect(SourceCode) ? "Auto-detect"
        : LanguageCatalog.GetDisplayLabel(SourceCode);

    /// <summary>Sub-hint under the source label (spec §3).</summary>
    public string SourceSubHint =>
        LanguageCatalog.IsKeyboardLayout(SourceCode)
            ? DetectedKeyboardLayout is { } layout
                ? $"Detected: {layout.FriendlyName}"
                : "Layout detection unavailable — auto-detecting instead"
        : LanguageCatalog.IsAutoDetect(SourceCode)
            ? "Let the model identify the language"
            : "Spoken language";

    /// <summary>
    /// Resting label for the target card. While translation is off the output
    /// matches the spoken language, so the card reads "Same as source" rather
    /// than echoing a stale target that contradicts the summary line.
    /// </summary>
    public string TargetDisplayLabel =>
        TranslationEnabled && !LanguageCatalog.IsNoTranslation(TargetCode)
            ? LanguageCatalog.GetDisplayLabel(TargetCode)
            : "Same as source";

    /// <summary>Plain-language restatement of the relationship (spec §7).</summary>
    public string SummaryText
    {
        get
        {
            var spoken =
                LanguageCatalog.IsKeyboardLayout(SourceCode)
                    ? DetectedKeyboardLayout is { } layout
                        ? LanguageCatalog.GetEnglishName(layout.LanguageCode)
                        : "your keyboard language"
                : LanguageCatalog.IsAutoDetect(SourceCode)
                    ? "any language"
                    : LanguageCatalog.GetEnglishName(SourceCode);

            var translating = TranslationEnabled
                              && TargetForm != TranslationForm.None
                              && !IsTranslationPaused;
            var typed = translating
                ? LanguageCatalog.GetEnglishName(TargetCode)
                : IsTranslationPaused
                    ? $"{spoken} (translation paused)"
                    : $"{spoken} (no translation)";

            return $"You speak {spoken} → Parlotype types {typed}.";
        }
    }

    /// <summary>
    /// True when translation is on but the active Whisper model can't honour it
    /// (ADR-033/ADR-061). Toggle form only — full-form engines translate via
    /// prompt, and the none form is already locked. The preference itself stays
    /// on; only the effective behaviour is suspended, so every surface renders
    /// this as a reversible "paused", never as a silent "off".
    /// </summary>
    public bool IsTranslationPaused =>
        TargetForm == TranslationForm.Toggle
        && TranslationEnabled
        && !WhisperModelSupportsTranslation;

    /// <summary>Display name of the active Whisper model, for paused copy.</summary>
    public string WhisperModelDisplayName => WhisperModelInfo.Get(WhisperModel).DisplayName;

    /// <summary>
    /// Amber note shown wherever the paused state needs explaining, naming the
    /// offending model and the way out.
    /// </summary>
    public string TranslationPausedNote =>
        $"Translation is paused — \"{WhisperModelDisplayName}\" is transcription-only. " +
        "Pick Medium or Large v1/v2/v3 to resume.";

    /// <summary>Per-role MRU for the source picker's Recent cluster.</summary>
    public IReadOnlyList<string> SourceRecent => _sourceRecent;

    /// <summary>Per-role MRU for the target picker's Recent cluster.</summary>
    public IReadOnlyList<string> TargetRecent => _targetRecent;

    /// <summary>The selectable target languages for the active engine.</summary>
    public IReadOnlyList<LanguageInfo> TargetLanguages =>
        Capabilities.SupportsArbitraryTranslation
            ? LanguageCatalog.AllLanguages
            : Capabilities.FixedTranslationTargets;

    // ----- Initialization -------------------------------------------------

    /// <summary>
    /// Loads persisted state and applies the saved engine's capabilities.
    /// Fallback corrections run silently here — toasts are only for live
    /// engine/model switches, not startup reconciliation.
    /// </summary>
    public async Task InitializeAsync(CancellationToken ct = default)
    {
        if (_initialized)
            return;
        _initialized = true;

        // Idempotent legacy-settings migration (ADR-034).
        await LanguageSettingsMigrator.MigrateAsync(_settings, ct);

        SourceCode = await _settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, ct)
                     ?? LanguageCatalog.AutoDetectCode;
        TargetCode = await _settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage, ct)
                     ?? LanguageCatalog.NoTranslationCode;

        var translationEnabledStr = await _settings.GetAsync<string>(SettingsKeys.TranslationEnabled, ct);
        TranslationEnabled = bool.TryParse(translationEnabledStr, out var te) && te;

        _sourceRecent = (await _settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages, ct))?.ToList() ?? [];
        _targetRecent = (await _settings.GetAsync<List<string>>(SettingsKeys.RecentTargetLanguages, ct))?.ToList() ?? [];

        RefreshKeyboardLayout();

        var engineStr = await _settings.GetAsync<string>(SettingsKeys.SpeechEngine, ct);
        var engine = Enum.TryParse<SpeechEngine>(engineStr, ignoreCase: true, out var e) ? e : SpeechEngine.Parakeet;

        _suppressToasts = true;
        try
        {
            ApplyEngine(engine);
        }
        finally
        {
            _suppressToasts = false;
        }

        var modelStr = await _settings.GetAsync<string>(SettingsKeys.SelectedWhisperModel, ct);
        var model = Enum.TryParse<WhisperModelType>(modelStr, out var m) ? m : WhisperModelType.Base;

        // Startup reconciliation is silent — the paused state is rendered inline
        // on both surfaces; a toast is only for a live model switch.
        _suppressToasts = true;
        try
        {
            SetWhisperModel(model);
        }
        finally
        {
            _suppressToasts = false;
        }
    }

    /// <summary>
    /// Re-detects the OS keyboard layout (e.g. when a picker opens or on each live
    /// poll tick). Logs only when the detected layout actually changes, since this
    /// is called ~twice a second while a surface that displays it is visible.
    /// </summary>
    public void RefreshKeyboardLayout()
    {
        var previous = DetectedKeyboardLayout;
        var current = _keyboardLayout.Detect();
        if (current == previous)
            return;

        DetectedKeyboardLayout = current;
        _logger.LogDebug("Detected keyboard layout changed: {Code} ({Name})",
            current?.LanguageCode ?? "(none)", current?.FriendlyName ?? "(none)");
    }

    /// <summary>
    /// Registers interest in live keyboard-layout updates from a visible surface
    /// (the Language settings page or the Transcribe strip). Reference-counted, so
    /// the shared timer runs while at least one surface is visible. Balance every
    /// call with <see cref="EndLivePolling"/>.
    /// </summary>
    public void BeginLivePolling()
    {
        _livePollSubscribers++;
        UpdateLayoutPollState();
    }

    /// <summary>Releases a <see cref="BeginLivePolling"/> registration.</summary>
    public void EndLivePolling()
    {
        if (_livePollSubscribers > 0)
            _livePollSubscribers--;
        UpdateLayoutPollState();
    }

    /// <summary>Whether the live-poll timer is currently ticking (diagnostics/tests).</summary>
    public bool IsLayoutPollActive => _layoutPollTimer?.IsEnabled == true;

    // The poll only has work to do while the source is the keyboard sentinel —
    // any other source has no detected layout to keep current.
    private bool ShouldPollLayout =>
        _livePollSubscribers > 0 && LanguageCatalog.IsKeyboardLayout(SourceCode);

    private void UpdateLayoutPollState()
    {
        if (ShouldPollLayout)
        {
            _layoutPollTimer ??= CreateLayoutPollTimer();
            if (!_layoutPollTimer.IsEnabled)
            {
                RefreshKeyboardLayout(); // immediate, don't wait a full interval
                _layoutPollTimer.Start();
            }
        }
        else
        {
            _layoutPollTimer?.Stop();
        }
    }

    private DispatcherTimer CreateLayoutPollTimer()
    {
        var timer = new DispatcherTimer { Interval = LayoutPollInterval };
        timer.Tick += (_, _) => RefreshKeyboardLayout();
        return timer;
    }

    partial void OnSourceCodeChanged(string value) => UpdateLayoutPollState();

    // ----- Engine / model switches (spec §8 fallbacks) ---------------------

    /// <summary>
    /// Applies a new engine's capabilities. Selections the new engine cannot
    /// honour fall back per spec §8, each explained by a one-line toast:
    /// unsupported source → keyboard layout; target form None → translation off;
    /// Toggle with a different resting target → forced to the single option;
    /// Full with an unknown target → reset to the default.
    /// </summary>
    public void SetEngine(SpeechEngine engine)
    {
        if (engine == Engine && _initialized)
            return;

        ApplyEngine(engine);
    }

    private void ApplyEngine(SpeechEngine engine)
    {
        Engine = engine;
        Capabilities = SpeechEngineCapabilities.For(engine);
        NotifyCapabilityDerived();

        // Engines with no language choices (Parakeet) ignore the language
        // settings entirely and show no language UI, so nothing needs to fall
        // back — and deliberately nothing is persisted, so the user's source /
        // translation setup survives a round-trip through such an engine.
        if (!Capabilities.HasLanguageChoices)
            return;

        var engineName = EngineName(engine);

        // Source: an explicit language the new engine can't transcribe falls back
        // to the keyboard layout — always valid because it resolves to auto.
        if (!LanguageCatalog.IsAutoDetect(SourceCode)
            && !LanguageCatalog.IsKeyboardLayout(SourceCode)
            && !IsSupportedSource(SourceCode))
        {
            var previous = LanguageCatalog.GetEnglishName(SourceCode);
            SourceCode = LanguageCatalog.KeyboardLayoutCode;
            ShowToast($"{previous} isn't a source in {engineName}. Using your keyboard layout.");
            _ = PersistAsync(SettingsKeys.SelectedSourceLanguage, SourceCode);
        }

        switch (TargetForm)
        {
            case TranslationForm.None when TranslationEnabled:
                TranslationEnabled = false;
                ShowToast($"{engineName} can't translate — output now matches your spoken language.");
                _ = PersistTranslationStateAsync();
                break;

            case TranslationForm.Toggle:
                var only = Capabilities.FixedTranslationTargets[0].Code;
                if (!LanguageCatalog.IsNoTranslation(TargetCode)
                    && !string.Equals(TargetCode, only, StringComparison.OrdinalIgnoreCase))
                {
                    var previous = LanguageCatalog.GetEnglishName(TargetCode);
                    TargetCode = only;
                    // Only explain when the change is user-visible right now; a
                    // resting target behind a disabled connector resets silently.
                    if (TranslationEnabled)
                        ShowToast($"{previous} isn't available in {engineName}. " +
                                  $"Translation set to {LanguageCatalog.GetEnglishName(only)}.");
                    _ = PersistTranslationStateAsync();
                }
                break;

            case TranslationForm.Full:
                if (!LanguageCatalog.IsNoTranslation(TargetCode)
                    && LanguageCatalog.TryGet(TargetCode) is null)
                {
                    TargetCode = DefaultTargetCode();
                    if (TranslationEnabled)
                        ShowToast($"Previous target reset — not supported by {engineName}.");
                    _ = PersistTranslationStateAsync();
                }
                break;
        }
    }

    /// <summary>
    /// Applies the active Whisper model (ADR-033). A switch that newly pauses a
    /// live translation is explained by a toast, so the cause is learned at the
    /// moment the model is chosen rather than from silent output later.
    /// </summary>
    public void SetWhisperModel(WhisperModelType model)
    {
        var wasPaused = IsTranslationPaused;

        WhisperModel = model;
        WhisperModelSupportsTranslation = WhisperModelInfo.Get(model).SupportsTranslation;

        if (IsTranslationPaused && !wasPaused)
        {
            ShowToast($"\"{WhisperModelDisplayName}\" can't translate — translation is " +
                      "paused until you pick a multilingual model.");
        }
    }

    // ----- Transitions (spec §7) -------------------------------------------

    /// <summary>
    /// Flips translation on/off — the connector / switch / strip action. On
    /// re-enable the resting target is restored; first enable picks the engine
    /// default so no extra selection is needed (spec §9). No-op when the engine
    /// cannot translate (locked connector).
    /// </summary>
    public void ToggleTranslation()
    {
        if (TargetForm == TranslationForm.None)
            return;

        TranslationEnabled = !TranslationEnabled;

        if (TranslationEnabled && LanguageCatalog.IsNoTranslation(TargetCode))
            TargetCode = DefaultTargetCode();

        _logger.LogInformation("Translation toggled: {Enabled}, target={Target}", TranslationEnabled, TargetCode);
        _ = PersistTranslationStateAsync();
        RelationshipChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Selects the source: the keyboard / auto sentinels or an explicit language
    /// code. Explicit languages are promoted into the source MRU.
    /// </summary>
    public void SelectSource(string code)
    {
        if (string.Equals(code, SourceCode, StringComparison.OrdinalIgnoreCase))
            return;

        SourceCode = code;
        PromoteRecent(ref _sourceRecent, code, SettingsKeys.RecentSourceLanguages, out var mru);
        _logger.LogInformation("Source language selected: {Code}", code);
        _ = PersistAsync(SettingsKeys.SelectedSourceLanguage, code, mru is null ? null : SettingsKeys.RecentSourceLanguages, mru);
        OnPropertyChanged(nameof(SourceRecent));
        RelationshipChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Selects the target. A real language code also turns translation on (the
    /// full picker's rows imply intent); <see cref="LanguageCatalog.NoTranslationCode"/>
    /// — the picker's "Off" row — turns translation off and keeps the resting
    /// target for later restore.
    /// </summary>
    public void SelectTarget(string code)
    {
        if (LanguageCatalog.IsNoTranslation(code))
        {
            if (!TranslationEnabled)
                return;
            TranslationEnabled = false;
            _logger.LogInformation("Translation turned off via target picker");
            _ = PersistTranslationStateAsync();
            RelationshipChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        var unchanged = string.Equals(code, TargetCode, StringComparison.OrdinalIgnoreCase) && TranslationEnabled;
        if (unchanged)
            return;

        TargetCode = code;
        TranslationEnabled = true;
        PromoteRecent(ref _targetRecent, code, SettingsKeys.RecentTargetLanguages, out var mru);
        _logger.LogInformation("Target language selected: {Code}", code);
        _ = PersistTranslationStateAsync(mru is null ? null : SettingsKeys.RecentTargetLanguages, mru);
        OnPropertyChanged(nameof(TargetRecent));
        RelationshipChanged?.Invoke(this, EventArgs.Empty);
    }

    // ----- Internals -------------------------------------------------------

    private bool IsSupportedSource(string code) =>
        Capabilities.EffectiveSourceLanguages.Any(
            l => string.Equals(l.Code, code, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// The target used when translation is first enabled: the single fixed target
    /// on toggle engines, the most-recent (falling back to English) on full ones.
    /// </summary>
    private string DefaultTargetCode() =>
        Capabilities.SupportsArbitraryTranslation
            ? _targetRecent.FirstOrDefault() ?? LanguageCatalog.EnglishCode
            : Capabilities.FixedTranslationTargets.Count > 0
                ? Capabilities.FixedTranslationTargets[0].Code
                : LanguageCatalog.EnglishCode;

    private void PromoteRecent(ref List<string> recent, string code, string key, out List<string>? changed)
    {
        var updated = RecentLanguages.Add(recent, code).ToList();
        changed = updated.SequenceEqual(recent, StringComparer.OrdinalIgnoreCase) ? null : updated;
        if (changed is not null)
            recent = changed;
    }

    private void NotifyCapabilityDerived()
    {
        OnPropertyChanged(nameof(Capabilities));
        OnPropertyChanged(nameof(TargetForm));
        OnPropertyChanged(nameof(HasLanguageChoices));
        OnPropertyChanged(nameof(Connector));
        OnPropertyChanged(nameof(ConnectorGlyph));
        OnPropertyChanged(nameof(SummaryText));
        OnPropertyChanged(nameof(TargetLanguages));
        OnPropertyChanged(nameof(IsTranslationPaused));
        OnPropertyChanged(nameof(ConnectorTooltip));
        OnPropertyChanged(nameof(IsToggleForm));
        OnPropertyChanged(nameof(IsFullForm));
        OnPropertyChanged(nameof(IsNoneForm));
        OnPropertyChanged(nameof(IsConnectorOn));
        OnPropertyChanged(nameof(IsConnectorOff));
        OnPropertyChanged(nameof(IsConnectorLocked));
        OnPropertyChanged(nameof(IsConnectorPaused));
        OnPropertyChanged(nameof(ToggleSwitchLabel));
        OnPropertyChanged(nameof(UnavailableNote));
        OnPropertyChanged(nameof(TargetDisplayLabel));
    }

    private static string EngineName(SpeechEngine engine) => engine switch
    {
        SpeechEngine.Whisper => "Whisper",
        SpeechEngine.Gemma4 => "Gemma 4",
        SpeechEngine.Parakeet => "Parakeet",
        _ => engine.ToString(),
    };

    private void ShowToast(string message)
    {
        if (_suppressToasts)
            return;

        _toastCts?.Cancel();
        var cts = _toastCts = new CancellationTokenSource();
        ToastMessage = message;
        _ = ClearToastAfterDelayAsync(cts.Token);
    }

    private async Task ClearToastAfterDelayAsync(CancellationToken ct)
    {
        try
        {
            await Task.Delay(ToastDuration, ct);
            // Property changes must reach Avalonia bindings on the UI thread.
            await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => ToastMessage = null);
        }
        catch (OperationCanceledException)
        {
            // Superseded by a newer toast.
        }
    }

    private async Task PersistAsync(string key, string value, string? mruKey = null, List<string>? mru = null)
    {
        try
        {
            await _settings.SetAsync(key, value);
            if (mruKey is not null && mru is not null)
                await _settings.SetAsync(mruKey, mru);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist language setting {Key}", key);
        }
    }

    private async Task PersistTranslationStateAsync(string? mruKey = null, List<string>? mru = null)
    {
        try
        {
            await _settings.SetAsync(SettingsKeys.TranslationEnabled, TranslationEnabled.ToString());
            await _settings.SetAsync(SettingsKeys.SelectedTargetLanguage, TargetCode);
            if (mruKey is not null && mru is not null)
                await _settings.SetAsync(mruKey, mru);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist translation state");
        }
    }
}
