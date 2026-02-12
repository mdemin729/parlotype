namespace Parlotype.Core.TextProcessing;

/// <summary>
/// Post-processes raw transcription text (punctuation, formatting, etc.).
/// </summary>
public interface ITextProcessor
{
    /// <summary>Processes raw transcribed text and returns the cleaned result.</summary>
    string Process(string rawText);
}
