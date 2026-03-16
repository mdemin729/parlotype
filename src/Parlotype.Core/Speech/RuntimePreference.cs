namespace Parlotype.Core.Speech;

/// <summary>
/// Controls which Whisper.net runtime backend to prefer.
/// Must be configured before the first WhisperFactory is created.
/// </summary>
public enum RuntimePreference
{
    /// <summary>Try GPU (CUDA) first, fall back to CPU if unavailable.</summary>
    Auto,

    /// <summary>Force CPU-only inference, ignoring any available GPU.</summary>
    Cpu
}
