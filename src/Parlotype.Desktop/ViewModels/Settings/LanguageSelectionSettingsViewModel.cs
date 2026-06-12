using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Settings → Language page: a thin presentation wrapper over the shared
/// <see cref="LanguageRelationshipViewModel"/> (which owns all state,
/// persistence, and fallback logic — see spec §7/§8). This VM contributes only
/// what is page-specific: the two picker popovers, the target's form-dependent
/// rendering flags (toggle / full / none), the connector CSS-class booleans,
/// and recording-stop on selection changes.
/// </summary>
public partial class LanguageSelectionSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ILogger<LanguageSelectionSettingsViewModel> _logger;

    public override string Title => "Language";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;

    /// <summary>The shared source → target relationship (state + derivations).</summary>
    public LanguageRelationshipViewModel Relationship { get; }

    public LanguagePickerViewModel SourcePicker { get; }
    public LanguagePickerViewModel TargetPicker { get; }

    public LanguageSelectionSettingsViewModel(
        LanguageRelationshipViewModel relationship,
        TranscribeViewModel? transcribeViewModel = null,
        ILogger<LanguageSelectionSettingsViewModel>? logger = null)
    {
        Relationship = relationship;
        _transcribeViewModel = transcribeViewModel;
        _logger = logger ?? NullLogger<LanguageSelectionSettingsViewModel>.Instance;

        SourcePicker = new LanguagePickerViewModel(
            header: "You speak",
            getSupported: () => Relationship.Capabilities.EffectiveSourceLanguages,
            getRecents: () => Relationship.SourceRecent,
            getSelectedCode: () => Relationship.SourceCode,
            onSelect: SelectSource,
            getSpecials: BuildSourceSpecials);

        TargetPicker = new LanguagePickerViewModel(
            header: "Translate to",
            getSupported: () => Relationship.TargetLanguages,
            getRecents: () => Relationship.TargetRecent,
            getSelectedCode: () => Relationship.TranslationEnabled
                ? Relationship.TargetCode
                : LanguageCatalog.NoTranslationCode,
            onSelect: SelectTarget,
            getSpecials: () =>
                [new LanguageSpecialRow(
                    LanguageCatalog.NoTranslationCode, "Off — no translation",
                    SubHint: null, LanguageRowIcon.Off)]);

        Relationship.PropertyChanged += OnRelationshipPropertyChanged;
        Relationship.RelationshipChanged += OnRelationshipChanged;

        // Fire-and-forget — idempotent; log faults so a corrupt settings.json
        // doesn't fail silently. (The relationship may already be initialized by
        // another surface.)
        _ = Relationship.InitializeAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Language settings initialization failed"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    // ----- Form / connector rendering flags --------------------------------

    /// <summary>Whisper-style: exactly off + one fixed target ⇒ a switch, no list.</summary>
    public bool IsToggleForm => Relationship.TargetForm == TranslationForm.Toggle;

    /// <summary>LLM-style arbitrary targets ⇒ picker button + popover.</summary>
    public bool IsFullForm => Relationship.TargetForm == TranslationForm.Full;

    /// <summary>Engine can't translate ⇒ disabled card + amber note + locked connector.</summary>
    public bool IsNoneForm => Relationship.TargetForm == TranslationForm.None;

    public bool IsConnectorOn => Relationship.Connector == ConnectorState.On;
    public bool IsConnectorOff => Relationship.Connector == ConnectorState.Off;
    public bool IsConnectorLocked => Relationship.Connector == ConnectorState.Locked;

    /// <summary>
    /// Two-way surface for the toggle-form switch. Routed through
    /// <see cref="LanguageRelationshipViewModel.ToggleTranslation"/> so default
    /// targets and persistence apply.
    /// </summary>
    public bool TranslationSwitch
    {
        get => Relationship.TranslationEnabled;
        set
        {
            if (value != Relationship.TranslationEnabled)
                Relationship.ToggleTranslation();
        }
    }

    /// <summary>Label for the toggle-form switch (e.g. "Translate to English").</summary>
    public string ToggleSwitchLabel =>
        Relationship.Capabilities.FixedTranslationTargets.Count > 0
            ? $"Translate to {LanguageCatalog.GetEnglishName(Relationship.Capabilities.FixedTranslationTargets[0].Code)}"
            : "Translate";

    /// <summary>Amber inline note for the none form, naming the model (spec §4).</summary>
    public string UnavailableNote =>
        $"{Relationship.EngineDisplayName} can't translate — Parlotype types exactly what you say.";

    /// <summary>Sub-hint under the full-form target field.</summary>
    public string TargetSubHint =>
        Relationship.TranslationEnabled ? "Translation target" : "Off — no translation";

    /// <summary>Tile glyph for the source field (mirrors the picker rows).</summary>
    public string SourceTileText =>
        LanguageCatalog.IsKeyboardLayout(Relationship.SourceCode) ? "⌨"
        : LanguageCatalog.IsAutoDetect(Relationship.SourceCode) ? "✦"
        : Relationship.SourceCode.ToUpperInvariant();

    /// <summary>Tile glyph for the full-form target field.</summary>
    public string TargetTileText =>
        Relationship.TranslationEnabled && !LanguageCatalog.IsNoTranslation(Relationship.TargetCode)
            ? Relationship.TargetCode.ToUpperInvariant()
            : "⊘";

    public string TranslationPausedNote =>
        "Translation is paused — the selected Whisper model can't translate. " +
        "Pick Medium or Large v1/v2/v3 to resume.";

    // ----- Engine / model hooks (called by SettingsWindowViewModel) ---------

    /// <summary>
    /// Applies a new engine: capability swap + spec §8 fallbacks/toasts happen
    /// in the relationship VM; this layer refreshes the pickers and collapses
    /// any popover that no longer fits the new form.
    /// </summary>
    public void UpdateForEngine(SpeechEngine engine)
    {
        Relationship.SetEngine(engine);

        SourcePicker.Refresh();
        TargetPicker.Refresh();

        if (TargetPicker.IsOpen && !IsFullForm)
            TargetPicker.IsOpen = false;
    }

    /// <summary>Updates the Whisper-model translation-availability flag (ADR-033).</summary>
    public void UpdateTranslationAvailability(WhisperModelType model) =>
        Relationship.SetWhisperModel(model);

    // ----- Popover commands --------------------------------------------------

    [RelayCommand]
    private void OpenSourcePicker()
    {
        var willOpen = !SourcePicker.IsOpen;
        TargetPicker.IsOpen = false;

        if (willOpen)
        {
            // Fresh detection so the keyboard special's sub-hint is current.
            Relationship.RefreshKeyboardLayout();
            SourcePicker.Filter = "";
            SourcePicker.Refresh();
        }

        SourcePicker.IsOpen = willOpen;
    }

    [RelayCommand]
    private void OpenTargetPicker()
    {
        if (!IsFullForm)
            return;

        var willOpen = !TargetPicker.IsOpen;
        SourcePicker.IsOpen = false;

        if (willOpen)
        {
            TargetPicker.Filter = "";
            TargetPicker.Refresh();
        }

        TargetPicker.IsOpen = willOpen;
    }

    /// <summary>Connector click — the single-action translation flip (spec §7).</summary>
    [RelayCommand]
    private void ToggleTranslation()
    {
        Relationship.ToggleTranslation();

        if (!Relationship.TranslationEnabled)
            TargetPicker.IsOpen = false;
    }

    // ----- Internals -----------------------------------------------------------

    private void SelectSource(string code)
    {
        Relationship.SelectSource(code);
        SourcePicker.IsOpen = false;
        SourcePicker.Refresh();
    }

    private void SelectTarget(string code)
    {
        Relationship.SelectTarget(code);
        TargetPicker.IsOpen = false;
        TargetPicker.Refresh();
    }

    private IReadOnlyList<LanguageSpecialRow> BuildSourceSpecials()
    {
        var specials = new List<LanguageSpecialRow>
        {
            new(LanguageCatalog.KeyboardLayoutCode,
                "System keyboard layout",
                Relationship.DetectedKeyboardLayout is { } layout
                    ? $"Detected: {layout.FriendlyName}"
                    : "Layout detection unavailable",
                LanguageRowIcon.Keyboard),
        };

        if (Relationship.Capabilities.SupportsAutoDetect)
        {
            specials.Add(new LanguageSpecialRow(
                LanguageCatalog.AutoDetectCode,
                "Auto-detect",
                "Let the model identify the language",
                LanguageRowIcon.Sparkle));
        }

        return specials;
    }

    private void OnRelationshipPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(LanguageRelationshipViewModel.TranslationEnabled):
                OnPropertyChanged(nameof(TranslationSwitch));
                OnPropertyChanged(nameof(IsConnectorOn));
                OnPropertyChanged(nameof(IsConnectorOff));
                OnPropertyChanged(nameof(TargetSubHint));
                OnPropertyChanged(nameof(TargetTileText));
                break;

            case nameof(LanguageRelationshipViewModel.SourceCode):
                OnPropertyChanged(nameof(SourceTileText));
                break;

            case nameof(LanguageRelationshipViewModel.TargetCode):
                OnPropertyChanged(nameof(TargetTileText));
                break;

            case nameof(LanguageRelationshipViewModel.Capabilities):
            case nameof(LanguageRelationshipViewModel.Engine):
                OnPropertyChanged(nameof(IsToggleForm));
                OnPropertyChanged(nameof(IsFullForm));
                OnPropertyChanged(nameof(IsNoneForm));
                OnPropertyChanged(nameof(IsConnectorOn));
                OnPropertyChanged(nameof(IsConnectorOff));
                OnPropertyChanged(nameof(IsConnectorLocked));
                OnPropertyChanged(nameof(ToggleSwitchLabel));
                OnPropertyChanged(nameof(UnavailableNote));
                break;
        }
    }

    private void OnRelationshipChanged(object? sender, EventArgs e)
    {
        _ = StopRecordingIfActiveAsync();
    }

    private async Task StopRecordingIfActiveAsync()
    {
        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording after language change");
            await _transcribeViewModel.StopRecordingAsync();
        }
    }
}
