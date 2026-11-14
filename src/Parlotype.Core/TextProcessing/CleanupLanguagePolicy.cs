using Parlotype.Core.Speech;

namespace Parlotype.Core.TextProcessing;

/// <summary>What is known, before anyone speaks, about the language the pasted text will be in.</summary>
public enum CleanupLanguageKind
{
    /// <summary>
    /// The settings do not say: the engine detects the language per utterance.
    /// The decision is made for each transcript (see <see cref="SpeechCleanupGate"/>).
    /// </summary>
    Unknown,

    /// <summary>The text will be English: it was chosen, or it is translated into English.</summary>
    English,

    /// <summary>The text will be in another language, named by <see cref="CleanupLanguage.Code"/>.</summary>
    NotEnglish,
}

/// <summary>
/// The language the pasted text will be in, as far as the settings tell (ADR-075).
/// </summary>
/// <param name="Kind">English, another language, or not known in advance.</param>
/// <param name="Code">The language code when <paramref name="Kind"/> is <see cref="CleanupLanguageKind.NotEnglish"/>; otherwise null.</param>
public readonly record struct CleanupLanguage(CleanupLanguageKind Kind, string? Code = null)
{
    public static CleanupLanguage Unknown => new(CleanupLanguageKind.Unknown);

    public static CleanupLanguage English => new(CleanupLanguageKind.English);

    /// <summary>English for the English code, otherwise that language.</summary>
    public static CleanupLanguage Of(string code) =>
        CleanupLanguagePolicy.IsEnglish(code) ? English : new(CleanupLanguageKind.NotEnglish, code);
}

/// <summary>
/// Works out the output language of a dictation from the language settings, so
/// the English-only cleanup model (ADR-073) is paused when the text will not be
/// English instead of relying on the user to switch it off (ADR-075).
/// </summary>
/// <remarks>
/// Shared by the audio pipeline, which acts on it, and the desktop UI, which
/// shows it, so the two cannot disagree. It mirrors what each engine does with
/// the settings rather than what the settings say: a stored source language
/// means nothing to an engine that always auto-detects.
/// </remarks>
public static class CleanupLanguagePolicy
{
    /// <summary>
    /// The output language for <paramref name="engine"/> under the given settings.
    /// </summary>
    /// <param name="engine">The active speech engine.</param>
    /// <param name="whisperModel">The selected Whisper model; ignored for other engines.</param>
    /// <param name="sourceCode">The stored source language: a code, auto-detect, the keyboard sentinel, or null.</param>
    /// <param name="detectedLayout">The current keyboard layout, for the keyboard sentinel; null when unknown.</param>
    /// <param name="translationEnabled">The translation switch.</param>
    /// <param name="targetCode">The stored translation target.</param>
    public static CleanupLanguage Resolve(
        SpeechEngine engine,
        WhisperModelType whisperModel,
        string? sourceCode,
        KeyboardLayoutInfo? detectedLayout,
        bool translationEnabled,
        string? targetCode)
    {
        var capabilities = SpeechEngineCapabilities.For(engine);

        // Parakeet and the cloud engines ignore the stored languages entirely
        // (they survive an engine switch on purpose, ADR-042), so nothing is
        // known until the text exists.
        if (!capabilities.HasLanguageChoices)
            return CleanupLanguage.Unknown;

        switch (engine)
        {
            case SpeechEngine.Whisper:
            {
                var model = WhisperModelInfo.Get(whisperModel);

                // An English-only model writes English whatever the settings say.
                if (model.IsEnglishOnly)
                    return CleanupLanguage.English;

                // Same condition the pipeline uses to set the translate flag.
                if (translationEnabled && IsEnglish(targetCode) && model.SupportsTranslation)
                    return CleanupLanguage.English;

                return FromSource(
                    SourceLanguageResolver.Resolve(sourceCode, detectedLayout, LanguageCatalog.WhisperLanguages));
            }

            case SpeechEngine.Gemma4:
            {
                var source = SourceLanguageResolver.Resolve(sourceCode, detectedLayout);

                // Same condition LlamaCppSpeechRecognizer uses to pick the translation prompt.
                var translates = translationEnabled
                    && !string.IsNullOrWhiteSpace(targetCode)
                    && !LanguageCatalog.IsNoTranslation(targetCode)
                    && (LanguageCatalog.IsAutoDetect(source)
                        || !string.Equals(source, targetCode, StringComparison.OrdinalIgnoreCase));

                return translates ? CleanupLanguage.Of(targetCode!) : FromSource(source);
            }

            default:
                return CleanupLanguage.Unknown;
        }
    }

    /// <summary>
    /// Narrows an <see cref="CleanupLanguageKind.Unknown"/> plan with the
    /// language the recognizer reported for one utterance. A plan that is
    /// already known wins: under translation the recognizer reports the spoken
    /// language, not the language of the text.
    /// </summary>
    public static CleanupLanguage Refine(CleanupLanguage planned, string? detectedLanguage)
    {
        if (planned.Kind != CleanupLanguageKind.Unknown || string.IsNullOrWhiteSpace(detectedLanguage))
            return planned;

        return CleanupLanguage.Of(detectedLanguage.Trim());
    }

    /// <summary>True for the English language code, or the name some recognizers report instead.</summary>
    public static bool IsEnglish(string? code) =>
        string.Equals(code, LanguageCatalog.EnglishCode, StringComparison.OrdinalIgnoreCase)
        || string.Equals(code, "english", StringComparison.OrdinalIgnoreCase);

    private static CleanupLanguage FromSource(string resolvedSource) =>
        LanguageCatalog.IsAutoDetect(resolvedSource)
            ? CleanupLanguage.Unknown
            : CleanupLanguage.Of(resolvedSource);
}
