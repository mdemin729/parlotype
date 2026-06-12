namespace Parlotype.Core.Speech;

/// <summary>
/// The form the target-language control takes for a given engine, derived from
/// <see cref="LanguageCapabilities"/>. The UI morphs the target side of the
/// language relationship in place when the active engine changes.
/// </summary>
public enum TranslationForm
{
    /// <summary>
    /// The engine cannot translate at all. The target card is disabled with an
    /// explanatory note and the connector is locked to "=".
    /// </summary>
    None,

    /// <summary>
    /// Exactly two outcomes — disabled or one fixed target (e.g. Whisper →
    /// English). Rendered as a labelled switch, never a list.
    /// </summary>
    Toggle,

    /// <summary>
    /// Arbitrary translation targets (LLM-style). Rendered as a picker button
    /// opening a searchable popover.
    /// </summary>
    Full,
}
