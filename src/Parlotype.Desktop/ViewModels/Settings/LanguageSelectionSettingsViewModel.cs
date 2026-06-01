using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>Which picker, if any, is currently expanded under the source/target buttons.</summary>
public enum LanguagePickerKind { None, Source, Target }

/// <summary>
/// Source-language + target-language settings for the active speech engine, with
/// translation as a master toggle (the arrow between the two language buttons).
///
/// <para>The section owns the persisted state (selection codes, translation flag,
/// per-role MRU lists) and the engine-aware capability surface. The actual picker
/// UI (search box + list) is delegated to two <see cref="LanguagePickerViewModel"/>
/// instances exposed as <see cref="SourcePicker"/> and <see cref="TargetPicker"/>.</para>
///
/// <para>Translation has one source of truth: <see cref="TranslationEnabled"/>. The
/// pipeline reads it alongside <see cref="SelectedTargetCode"/>; for Whisper that
/// resolves to the legacy <c>TranslateToEnglish</c> flag (ADR-021/033), and for
/// Gemma 4 it gates the in-prompt translation instruction.</para>
/// </summary>
public partial class LanguageSelectionSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ILogger<LanguageSelectionSettingsViewModel> _logger;

    private LanguageCapabilities _capabilities = SpeechEngineCapabilities.For(SpeechEngine.Whisper);
    private List<string> _sourceRecent = [];
    private List<string> _targetRecent = [];
    private bool _initialized;

    public override string Title => "Language";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;

    public LanguagePickerViewModel SourcePicker { get; }
    public LanguagePickerViewModel TargetPicker { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSourcePickerOpen))]
    [NotifyPropertyChangedFor(nameof(IsTargetPickerOpen))]
    private LanguagePickerKind _openPicker = LanguagePickerKind.None;

    public bool IsSourcePickerOpen => OpenPicker == LanguagePickerKind.Source;
    public bool IsTargetPickerOpen => OpenPicker == LanguagePickerKind.Target;

    partial void OnOpenPickerChanged(LanguagePickerKind value)
    {
        SourcePicker.IsOpen = value == LanguagePickerKind.Source;
        TargetPicker.IsOpen = value == LanguagePickerKind.Target;
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SourceButtonLabel))]
    private string _selectedSourceCode = LanguageCatalog.AutoDetectCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TargetButtonLabel))]
    private string _selectedTargetCode = LanguageCatalog.NoTranslationCode;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTargetButtonEnabled))]
    [NotifyPropertyChangedFor(nameof(ShowTranslationPausedNote))]
    private bool _translationEnabled;

    /// <summary>
    /// Whether the active Whisper model can translate (ADR-033). Tracked so the
    /// "translation paused" note can appear when the user has translation on but
    /// the chosen model can't honour it. Always true on engines other than Whisper.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowTranslationPausedNote))]
    private bool _whisperModelSupportsTranslation = true;

    /// <summary>Pretty label for the source button (e.g. "Auto-detect", "Russian — Русский").</summary>
    public string SourceButtonLabel =>
        LanguageCatalog.IsAutoDetect(SelectedSourceCode)
            ? "Auto-detect"
            : LanguageCatalog.GetDisplayLabel(SelectedSourceCode);

    /// <summary>
    /// Pretty label for the target button. Shows the currently selected target even
    /// when translation is disabled, so the user can see what would be used if they
    /// re-enabled it. Defaults to "English" when nothing has been chosen yet.
    /// </summary>
    public string TargetButtonLabel =>
        LanguageCatalog.IsNoTranslation(SelectedTargetCode)
            ? "English"
            : LanguageCatalog.GetDisplayLabel(SelectedTargetCode);

    /// <summary>
    /// True when the target button accepts clicks. Disabled when translation is off —
    /// the arrow is the way to bring it back. Independent of model capability: a
    /// translation-incapable Whisper model surfaces the paused note instead of
    /// blocking interaction.
    /// </summary>
    public bool IsTargetButtonEnabled => TranslationEnabled;

    /// <summary>
    /// True when the user has translation on but the active Whisper model can't
    /// honour it (e.g. <c>Large v3 Turbo</c>). Drives the explanatory accent note.
    /// Not shown on engines that translate via prompt (Gemma 4).
    /// </summary>
    public bool ShowTranslationPausedNote =>
        !_capabilities.SupportsArbitraryTranslation
        && TranslationEnabled
        && !WhisperModelSupportsTranslation;

    public string TranslationPausedNote =>
        "Translation is paused — the selected Whisper model can't translate. " +
        "Pick Medium or Large v1/v2/v3 to resume.";

    public LanguageSelectionSettingsViewModel(
        ISettingsService settings,
        TranscribeViewModel? transcribeViewModel = null,
        ILogger<LanguageSelectionSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _transcribeViewModel = transcribeViewModel;
        _logger = logger ?? NullLogger<LanguageSelectionSettingsViewModel>.Instance;

        SourcePicker = new LanguagePickerViewModel(
            header: "Select source language",
            getSupported: () => _capabilities.EffectiveSourceLanguages,
            getRecents: () => _sourceRecent,
            getSelectedCode: () => SelectedSourceCode,
            onSelect: SelectSource,
            getLeadingSentinel: () => _capabilities.SupportsAutoDetect
                ? ((string Code, string Label)?)(LanguageCatalog.AutoDetectCode, "Auto-detect")
                : null);

        TargetPicker = new LanguagePickerViewModel(
            header: "Select target language",
            getSupported: GetTargetLanguageList,
            getRecents: () => _targetRecent,
            getSelectedCode: () => SelectedTargetCode,
            onSelect: SelectTarget,
            getLeadingSentinel: () => null);

        // Fire-and-forget — log faults so a corrupt settings.json doesn't fail silently.
        _ = InitializeAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Language settings initialization failed"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    private IReadOnlyList<LanguageInfo> GetTargetLanguageList() =>
        _capabilities.SupportsArbitraryTranslation
            ? LanguageCatalog.AllLanguages
            : _capabilities.FixedTranslationTargets;

    private async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        // Idempotent — picks up legacy TranslateToEnglish / RecentLanguages on first run.
        await LanguageSettingsMigrator.MigrateAsync(_settings);

        SelectedSourceCode = await _settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage)
                             ?? LanguageCatalog.AutoDetectCode;
        SelectedTargetCode = await _settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage)
                             ?? LanguageCatalog.NoTranslationCode;

        var translationEnabledStr = await _settings.GetAsync<string>(SettingsKeys.TranslationEnabled);
        TranslationEnabled = bool.TryParse(translationEnabledStr, out var te) && te;

        _sourceRecent = (await _settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages))?.ToList() ?? [];
        _targetRecent = (await _settings.GetAsync<List<string>>(SettingsKeys.RecentTargetLanguages))?.ToList() ?? [];

        var engineStr = await _settings.GetAsync<string>(SettingsKeys.SpeechEngine);
        var engine = Enum.TryParse<SpeechEngine>(engineStr, ignoreCase: true, out var e) ? e : SpeechEngine.Whisper;
        UpdateForEngine(engine);

        var modelStr = await _settings.GetAsync<string>(SettingsKeys.SelectedWhisperModel);
        var model = Enum.TryParse<WhisperModelType>(modelStr, out var m) ? m : WhisperModelType.Base;
        UpdateTranslationAvailability(model);
    }

    /// <summary>
    /// Recomputes the source/target pickers for the given engine. Called from the
    /// settings window when the active engine changes.
    /// </summary>
    public void UpdateForEngine(SpeechEngine engine)
    {
        _capabilities = SpeechEngineCapabilities.For(engine);
        // Only ShowTranslationPausedNote reads _capabilities; button labels depend
        // on the selection codes which haven't changed here.
        OnPropertyChanged(nameof(ShowTranslationPausedNote));

        SourcePicker.Refresh();
        TargetPicker.Refresh();

        // If the open picker no longer fits the new engine (e.g. target picker open
        // but translation is off / the engine offers nothing), collapse it.
        if (OpenPicker == LanguagePickerKind.Target && !IsTargetButtonEnabled)
            OpenPicker = LanguagePickerKind.None;
    }

    /// <summary>
    /// Updates the Whisper-model-derived translation-availability flag (ADR-033).
    /// Has no effect on the user's saved <see cref="TranslationEnabled"/> intent —
    /// only the paused-note visibility.
    /// </summary>
    public void UpdateTranslationAvailability(WhisperModelType model) =>
        WhisperModelSupportsTranslation = WhisperModelInfo.Get(model).SupportsTranslation;

    [RelayCommand]
    private void OpenSourcePicker() =>
        TogglePicker(LanguagePickerKind.Source, gate: true, picker: SourcePicker);

    [RelayCommand]
    private void OpenTargetPicker() =>
        TogglePicker(LanguagePickerKind.Target, gate: IsTargetButtonEnabled, picker: TargetPicker);

    private void TogglePicker(LanguagePickerKind kind, bool gate, LanguagePickerViewModel picker)
    {
        if (!gate)
            return;

        var willOpen = OpenPicker != kind;
        OpenPicker = willOpen ? kind : LanguagePickerKind.None;

        // Clear the filter on open so previous search state doesn't carry over.
        // The Filter setter triggers a list rebuild when the value actually changes;
        // when it was already empty we rely on the picker's existing items (kept
        // current via SelectSource/SelectTarget + UpdateForEngine).
        if (willOpen)
            picker.Filter = "";
    }

    [RelayCommand]
    private void ToggleTranslation()
    {
        TranslationEnabled = !TranslationEnabled;

        if (TranslationEnabled)
        {
            // On first enable, ensure a real target is set.
            // Whisper has only one target (English); Gemma 4 prefers the most-recent.
            if (LanguageCatalog.IsNoTranslation(SelectedTargetCode))
                SelectedTargetCode = DefaultTargetForCurrentEngine();
        }
        else if (OpenPicker == LanguagePickerKind.Target)
        {
            // The target button just went disabled; collapse its picker.
            OpenPicker = LanguagePickerKind.None;
        }

        _logger.LogInformation("Translation toggled: {Enabled}, target={Target}", TranslationEnabled, SelectedTargetCode);
        _ = PersistTranslationStateAsync();
    }

    private string DefaultTargetForCurrentEngine() =>
        _capabilities.SupportsArbitraryTranslation
            ? _targetRecent.FirstOrDefault() ?? LanguageCatalog.EnglishCode
            : LanguageCatalog.EnglishCode;

    private void SelectSource(string code) =>
        SelectInto(
            code,
            current: SelectedSourceCode,
            apply: c => SelectedSourceCode = c,
            recent: _sourceRecent,
            updateRecent: list => _sourceRecent = list,
            picker: SourcePicker,
            settingsKey: SettingsKeys.SelectedSourceLanguage,
            mruKey: SettingsKeys.RecentSourceLanguages);

    private void SelectTarget(string code) =>
        SelectInto(
            code,
            current: SelectedTargetCode,
            apply: c => SelectedTargetCode = c,
            recent: _targetRecent,
            updateRecent: list => _targetRecent = list,
            picker: TargetPicker,
            settingsKey: SettingsKeys.SelectedTargetLanguage,
            mruKey: SettingsKeys.RecentTargetLanguages);

    /// <summary>
    /// Shared logic for source/target selection: dedupe-no-op, promote the role MRU,
    /// persist, refresh the picker, and collapse the open picker.
    /// </summary>
    private void SelectInto(
        string code,
        string current,
        Action<string> apply,
        List<string> recent,
        Action<List<string>> updateRecent,
        LanguagePickerViewModel picker,
        string settingsKey,
        string mruKey)
    {
        if (string.Equals(code, current, StringComparison.OrdinalIgnoreCase))
        {
            OpenPicker = LanguagePickerKind.None;
            return;
        }

        _logger.LogInformation("Language selected ({Key}): {Code}", settingsKey, code);
        apply(code);

        var updated = RecentLanguages.Add(recent, code).ToList();
        var recentChanged = !updated.SequenceEqual(recent, StringComparer.OrdinalIgnoreCase);
        if (recentChanged)
            updateRecent(updated);

        _ = PersistAsync(settingsKey, code, recentChanged ? mruKey : null, recentChanged ? updated : null);
        picker.Refresh();
        OpenPicker = LanguagePickerKind.None;
    }

    private async Task PersistAsync(string key, string code, string? mruKey, List<string>? mru)
    {
        await _settings.SetAsync(key, code);
        if (mruKey is not null && mru is not null)
            await _settings.SetAsync(mruKey, mru);

        await StopRecordingIfActiveAsync(key);
    }

    private async Task PersistTranslationStateAsync()
    {
        await _settings.SetAsync(SettingsKeys.TranslationEnabled, TranslationEnabled.ToString());
        await _settings.SetAsync(SettingsKeys.SelectedTargetLanguage, SelectedTargetCode);

        await StopRecordingIfActiveAsync(SettingsKeys.TranslationEnabled);
    }

    private async Task StopRecordingIfActiveAsync(string key)
    {
        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording after language change ({Key})", key);
            await _transcribeViewModel.StopRecordingAsync();
        }
    }
}
