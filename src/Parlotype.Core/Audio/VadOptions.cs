namespace Parlotype.Core.Audio;

/// <summary>Configuration options for Voice Activity Detection.</summary>
public sealed record VadOptions
{
    /// <summary>Speech detection confidence threshold (0.0-1.0). Lower = more sensitive.</summary>
    public float Threshold { get; init; } = 0.5f;

    /// <summary>Padding in milliseconds added before and after each speech segment.</summary>
    public int SpeechPadMs { get; init; } = 400;

    /// <summary>Minimum silence duration (ms) to split speech segments. Higher = fewer splits.</summary>
    public int MinSilenceDurationMs { get; init; } = 500;

    /// <summary>Minimum speech duration (ms) to count as a valid segment.</summary>
    public int MinSpeechDurationMs { get; init; } = 50;

    /// <summary>
    /// Duration in milliseconds of silence to insert between concatenated speech segments.
    /// This preserves prosodic context that Whisper needs for accurate recognition.
    /// Set to 0 to disable (original behavior). Default 160ms (2560 samples at 16kHz).
    /// </summary>
    public int InterSegmentSilenceMs { get; init; } = 160;
}
