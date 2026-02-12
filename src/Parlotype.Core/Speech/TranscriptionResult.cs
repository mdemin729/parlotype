namespace Parlotype.Core.Speech;

/// <summary>The result of a speech-to-text transcription.</summary>
public sealed record TranscriptionResult
{
    /// <summary>The recognized text.</summary>
    public required string Text { get; init; }

    /// <summary>Confidence score from 0.0 to 1.0, if available.</summary>
    public double? Confidence { get; init; }

    /// <summary>Language detected by the model, if available.</summary>
    public string? DetectedLanguage { get; init; }
}
