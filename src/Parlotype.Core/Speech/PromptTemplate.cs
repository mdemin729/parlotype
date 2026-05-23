namespace Parlotype.Core.Speech;

/// <summary>
/// A named, reusable transcription prompt for the Gemma 4 (llama.cpp) engine.
/// The <see cref="Text"/> may contain the <see cref="LanguageToken"/>
/// placeholder, which <see cref="Render"/> substitutes with a source-language
/// name at transcription time.
/// </summary>
public sealed record PromptTemplate(
    string Id,
    string Name,
    string Text,
    bool IsBuiltIn = false)
{
    /// <summary>
    /// Placeholder inside <see cref="Text"/> replaced with the source language
    /// when the prompt is rendered. A future shared setting will derive this
    /// from the active keyboard layout; for now <see cref="DefaultLanguage"/>
    /// is used.
    /// </summary>
    public const string LanguageToken = "{language}";

    /// <summary>
    /// Fallback source-language name substituted for <see cref="LanguageToken"/>
    /// until the keyboard-layout language setting exists. Single source of truth
    /// so that future work has one obvious seam to replace.
    /// </summary>
    public const string DefaultLanguage = "English";

    /// <summary>
    /// Returns <see cref="Text"/> with every <see cref="LanguageToken"/>
    /// replaced by <paramref name="language"/> (or <see cref="DefaultLanguage"/>
    /// when none is supplied).
    /// </summary>
    public string Render(string? language = null) =>
        Text.Replace(LanguageToken, string.IsNullOrWhiteSpace(language) ? DefaultLanguage : language, StringComparison.Ordinal);
}
