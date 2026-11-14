namespace Parlotype.Core.TextProcessing;

/// <summary>
/// The input format S1-mini was trained on: a fixed system prompt, then a user
/// message that opens with a control line and carries the transcript below it.
/// </summary>
/// <remarks>
/// None of this is tunable. The model card is explicit that a reworded system
/// prompt, a missing control line or a value outside the trained sets makes the
/// model hallucinate or produce garbled output, so the strings here are copied
/// from it verbatim and must stay that way.
/// </remarks>
public static class S1MiniPrompt
{
    /// <summary>The system prompt, exactly as published.</summary>
    public const string SystemPrompt =
        "You are a text normalizer for speech-to-text transcripts. The input begins with a control line " +
        "specifying the styling, structure, and context settings; clean the transcript to match those " +
        "settings and output only the cleaned text.";

    /// <summary>
    /// Builds the user message: the control line, a newline, then the transcript.
    /// </summary>
    public static string BuildUserMessage(SpeechCleanupOptions options, string transcript)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(transcript);

        return $"[Styling: {Value(options.Styling)}] [Structure: {Value(options.Structure)}] " +
               $"[Context: {Value(options.Context)}]\n{transcript}";
    }

    private static string Value(CleanupStyling styling) => styling switch
    {
        CleanupStyling.Casual => "casual",
        CleanupStyling.SemiCasual => "semi-casual",
        CleanupStyling.SemiFormal => "semi-formal",
        CleanupStyling.Formal => "formal",
        _ => throw new ArgumentOutOfRangeException(nameof(styling), styling, null),
    };

    private static string Value(CleanupStructure structure) => structure switch
    {
        CleanupStructure.Prose => "prose",
        CleanupStructure.Lists => "lists",
        _ => throw new ArgumentOutOfRangeException(nameof(structure), structure, null),
    };

    private static string Value(CleanupContext context) => context switch
    {
        CleanupContext.General => "general",
        CleanupContext.Email => "email",
        _ => throw new ArgumentOutOfRangeException(nameof(context), context, null),
    };
}
