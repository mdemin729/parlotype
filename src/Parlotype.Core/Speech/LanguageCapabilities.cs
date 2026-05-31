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
public sealed record LanguageCapabilities(
    bool SupportsAutoDetect,
    IReadOnlyList<LanguageInfo>? SupportedSourceLanguages,
    bool SupportsArbitraryTranslation,
    IReadOnlyList<LanguageInfo> FixedTranslationTargets)
{
    /// <summary>The effective source-language list (full list when unconstrained).</summary>
    public IReadOnlyList<LanguageInfo> EffectiveSourceLanguages =>
        SupportedSourceLanguages ?? LanguageCatalog.AllLanguages;
}

/// <summary>Resolves <see cref="LanguageCapabilities"/> for each speech engine.</summary>
public static class SpeechEngineCapabilities
{
    /// <summary>Returns the language capabilities for the given engine.</summary>
    public static LanguageCapabilities For(SpeechEngine engine) => engine switch
    {
        // Whisper: detects + transcribes its fixed ~99-language set. Translation is
        // English-only and handled by the existing "Translate to English" toggle, so
        // no arbitrary target picker is offered here.
        SpeechEngine.Whisper => new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: LanguageCatalog.WhisperLanguages,
            SupportsArbitraryTranslation: false,
            FixedTranslationTargets: []),

        // Gemma 4 (LLM): detects + transcribes, and can translate into any language
        // via the prompt, so the full target list is offered.
        SpeechEngine.Gemma4 => new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: null,
            SupportsArbitraryTranslation: true,
            FixedTranslationTargets: []),

        _ => new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: null,
            SupportsArbitraryTranslation: false,
            FixedTranslationTargets: []),
    };
}
