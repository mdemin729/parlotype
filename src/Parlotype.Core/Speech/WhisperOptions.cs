namespace Parlotype.Core.Speech;

/// <summary>Optional configuration for the Whisper speech recognizer.</summary>
public sealed record WhisperOptions
{
    /// <summary>Whisper model to use.</summary>
    public WhisperModelType Model { get; init; } = WhisperModelType.Base;

    /// <summary>Language code (e.g. "en", "ru") or "auto" for auto-detection.</summary>
    public string Language { get; init; } = "auto";

    /// <summary>Beam search size. 1 = greedy decoding.</summary>
    public int BeamSize { get; init; } = 1;

    /// <summary>Sampling temperature. 0.0 = deterministic.</summary>
    public float Temperature { get; init; } = 0.0f;

    /// <summary>Initial prompt for context hints.</summary>
    public string? InitialPrompt { get; init; }

    /// <summary>Number of CPU threads for inference. null = Whisper default.</summary>
    public int? Threads { get; init; }

    /// <summary>When true, translate non-English speech to English output.</summary>
    public bool TranslateToEnglish { get; init; }
    public RuntimePreference RuntimePreference { get; init; } = RuntimePreference.Auto;
}
