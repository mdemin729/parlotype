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
/// what is page-specific: the two picker popovers and a handful of tile/sub-hint
/// strings. Recording-stop on language changes is handled by
/// <see cref="TranscribeViewModel"/> itself via the relationship's
/// <see cref="LanguageRelationshipViewModel.RelationshipChanged"/> event.
/// </summary>
public partial class LanguageSelectionSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ILogger<LanguageSelectionSettingsViewModel> _logger;

    public override string Title => "Language";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;

    /// <summary>The shared source → target relationship (state + derivations).</summary>
    public LanguageRelationshipViewModel Relationship { get; }

    public LanguagePickerViewModel SourcePicker { get; }
    public LanguagePickerViewModel TargetPicker { get; }

    public LanguageSelectionSettingsViewModel(
        LanguageRelationshipViewModel relationship,
        ILogger<LanguageSelectionSettingsViewModel>? logger = null)
    {
        Relationship = relationship;
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

        // Fire-and-forget — idempotent; log faults so a corrupt settings.json
        // doesn't fail silently. (The relationship may already be initialized by
        // another surface.)
        _ = Relationship.InitializeAsync().ContinueWith(
            t => _logger.LogError(t.Exception, "Language settings initialization failed"),
            TaskContinuationOptions.OnlyOnFaulted);
    }

    // ----- Page-specific rendering helpers ----------------------------------
    // (Form flags, connector booleans, the switch surface, and the toggle/none
    // strings live on the shared Relationship VM — both surfaces bind there.)

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

        if (TargetPicker.IsOpen && !Relationship.IsFullForm)
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
        if (!Relationship.IsFullForm)
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
                OnPropertyChanged(nameof(TargetSubHint));
                OnPropertyChanged(nameof(TargetTileText));
                break;

            case nameof(LanguageRelationshipViewModel.SourceCode):
                OnPropertyChanged(nameof(SourceTileText));
                break;

            case nameof(LanguageRelationshipViewModel.TargetCode):
                OnPropertyChanged(nameof(TargetTileText));
                break;
        }
    }

}
