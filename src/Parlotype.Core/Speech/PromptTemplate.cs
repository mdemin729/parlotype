namespace Parlotype.Core.Speech;

/// <summary>
/// A named, reusable transcription prompt for the Gemma 4 (llama.cpp) engine.
/// </summary>
/// <remarks>
/// Bodies may contain two placeholders, substituted at transcription time:
/// <list type="bullet">
///   <item><see cref="SpeechLanguageToken"/> — the spoken (source) language name;
///   renders to <see cref="AutoDetectedLanguageName"/> when the source is
///   auto-detect.</item>
///   <item><see cref="TextLanguageToken"/> — the output (target) language name,
///   meaningful only when translating.</item>
/// </list>
/// Terminology map for maintainers: <c>speech</c> = source language,
/// <c>text</c> = target language.
///
/// <para>
/// Custom (user) prompts use a single body (<see cref="Text"/>). The
/// <strong>built-in default</strong> additionally carries
/// <see cref="TranslationText"/> (source + target) and
/// <see cref="AutoDetectText"/> (no tokens); the recognizer selects the body per
/// the resolved source/target pair. Both extra bodies are <see langword="null"/>
/// for custom prompts.
/// </para>
/// </remarks>
public sealed record PromptTemplate(
    string Id,
    string Name,
    string Text,
    bool IsBuiltIn = false,
    string? TranslationText = null,
    string? AutoDetectText = null)
{
    /// <summary>
    /// Placeholder replaced with the spoken (source) language name. Renders to
    /// <see cref="AutoDetectedLanguageName"/> when the source is auto-detect.
    /// </summary>
    public const string SpeechLanguageToken = "{speech_lang}";

    /// <summary>
    /// Placeholder replaced with the output (target) language name. Only present
    /// in translation bodies.
    /// </summary>
    public const string TextLanguageToken = "{text_lang}";

    /// <summary>
    /// Value substituted for <see cref="SpeechLanguageToken"/> when the source
    /// language is auto-detect (unknown at prompt-build time).
    /// </summary>
    public const string AutoDetectedLanguageName = "the detected language";

    /// <summary>
    /// Substitutes the language placeholders in <paramref name="body"/>. A
    /// <see langword="null"/> argument leaves the corresponding token untouched.
    /// </summary>
    public static string Substitute(string body, string? speechLanguage = null, string? textLanguage = null)
    {
        ArgumentNullException.ThrowIfNull(body);

        var result = body;
        if (speechLanguage is not null)
            result = result.Replace(SpeechLanguageToken, speechLanguage, StringComparison.Ordinal);
        if (textLanguage is not null)
            result = result.Replace(TextLanguageToken, textLanguage, StringComparison.Ordinal);
        return result;
    }

    /// <summary>
    /// Convenience over <see cref="Substitute(string, string?, string?)"/> applied
    /// to this prompt's <see cref="Text"/> body.
    /// </summary>
    public string Render(string? speechLanguage = null, string? textLanguage = null) =>
        Substitute(Text, speechLanguage, textLanguage);
}
