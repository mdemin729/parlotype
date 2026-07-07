namespace Parlotype.Core.Speech;

/// <summary>
/// Selects which speech recognition engine to use.
/// Persisted via <see cref="Settings.SettingsKeys.SpeechEngine"/>.
/// </summary>
public enum SpeechEngine
{
    /// <summary>Whisper.net — local, fast, well-tested. Default.</summary>
    Whisper,

    /// <summary>Gemma 4 via llama-server (llama.cpp) sidecar process.</summary>
    Gemma4,

    /// <summary>
    /// NVIDIA Parakeet TDT 0.6B v3 via sherpa-onnx, in-process. CPU-only,
    /// fastest engine; 25 European languages, always auto-detected;
    /// transcribe-only (no translation).
    /// </summary>
    Parakeet
}
