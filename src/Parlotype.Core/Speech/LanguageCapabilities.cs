namespace Parlotype.Core.Speech;

/// <summary>
/// Describes what a speech engine can do with source and target languages, so the
/// UI can offer only the choices that engine actually supports.
/// </summary>
/// <param name="SupportsAutoDetect">
/// Whether the engine can auto-detect the source language.
/// </param>
/// <param name="SupportedSourceLanguages">
/// The languages selectable as the source. <c>null</c> means "use the full list"
/// (<see cref="LanguageCatalog.AllLanguages"/>).
/// </param>
/// <param name="SupportsArbitraryTranslation">
/// When true, the engine can translate into any language (LLM-style), so the UI
/// shows a full target-language picker. When false, translation is limited to
/// <see cref="FixedTranslationTargets"/> (e.g. Whisper → English only).
/// </param>
/// <param name="FixedTranslationTargets">
/// The fixed set of translation targets when <see cref="SupportsArbitraryTranslation"/>
/// is false. Empty when the engine handles translation through a separate setting
/// (Whisper uses its existing "Translate to English" toggle).
/// </param>
/// <param name="SupportsSourceSelection">
/// Whether picking a source language has any effect. False for engines that
/// always auto-detect with no language-forcing parameter (e.g. Parakeet) —
/// <see cref="SupportedSourceLanguages"/> then only documents what the engine
/// can transcribe, and source pickers should not be offered.
/// </param>
public sealed record LanguageCapabilities(
    bool SupportsAutoDetect,
    IReadOnlyList<LanguageInfo>? SupportedSourceLanguages,
    bool SupportsArbitraryTranslation,
    IReadOnlyList<LanguageInfo> FixedTranslationTargets,
    bool SupportsSourceSelection = true)
{
    /// <summary>The effective source-language list (full list when unconstrained).</summary>
    public IReadOnlyList<LanguageInfo> EffectiveSourceLanguages =>
        SupportedSourceLanguages ?? LanguageCatalog.AllLanguages;

    /// <summary>
    /// Whether the engine offers any language choice at all — a selectable
    /// source or any translation control. When false, language UI (Settings
    /// page, Transcribe quick-picker strip) should be hidden entirely rather
    /// than shown with options the engine would silently ignore.
    /// </summary>
    public bool HasLanguageChoices =>
        SupportsSourceSelection || TranslationForm != TranslationForm.None;

    /// <summary>
    /// The form the target-language control takes: arbitrary translation ⇒
    /// <see cref="TranslationForm.Full"/>; a single fixed target ⇒
    /// <see cref="TranslationForm.Toggle"/>; otherwise the engine cannot
    /// translate ⇒ <see cref="TranslationForm.None"/>.
    /// </summary>
    public TranslationForm TranslationForm =>
        SupportsArbitraryTranslation ? TranslationForm.Full
        : FixedTranslationTargets.Count == 1 ? TranslationForm.Toggle
        : TranslationForm.None;
}

/// <summary>Resolves <see cref="LanguageCapabilities"/> for each speech engine.</summary>
public static class SpeechEngineCapabilities
{
    private static readonly IReadOnlyList<LanguageInfo> WhisperEnglishOnlyTargets =
        [LanguageCatalog.TryGet(LanguageCatalog.EnglishCode)!];


    /// <summary>Returns the language capabilities for the given engine.</summary>
    public static LanguageCapabilities For(SpeechEngine engine) => engine switch
    {
        // Whisper: detects + transcribes its fixed ~99-language set. Translation is
        // English-only (Whisper's translate task hard-codes the target). The target
        // picker offers English as the sole option so the unified UI on the Language
        // page can render it like any other selectable target.
        SpeechEngine.Whisper => new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: LanguageCatalog.WhisperLanguages,
            SupportsArbitraryTranslation: false,
            FixedTranslationTargets: WhisperEnglishOnlyTargets),

        // Gemma 4 (LLM): detects + transcribes, and can translate into any language
        // via the prompt, so the full target list is offered.
        SpeechEngine.Gemma4 => new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: null,
            SupportsArbitraryTranslation: true,
            FixedTranslationTargets: []),

        // Parakeet TDT v3: transcribe-only ASR. Always auto-detects among its 25
        // European languages — the model has no language-forcing parameter, so
        // no source selection is offered (the list only documents coverage) and
        // there is no translation task. HasLanguageChoices is false ⇒ language
        // UI is hidden entirely for this engine.
        SpeechEngine.Parakeet => new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: LanguageCatalog.ParakeetLanguages,
            SupportsArbitraryTranslation: false,
            FixedTranslationTargets: [],
            SupportsSourceSelection: false),

        // Fallback shape for any future transcribe-only engine: no arbitrary
        // translation and no fixed targets ⇒ TranslationForm.None, which the UI
        // renders as a disabled target with an explanatory note.
        _ => new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: null,
            SupportsArbitraryTranslation: false,
            FixedTranslationTargets: []),
    };
}
