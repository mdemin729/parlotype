namespace Parlotype.Core.Speech;

/// <summary>
/// Selects which speech recognition engine to use.
/// Persisted via <see cref="Settings.SettingsKeys.SpeechEngine"/>.
/// </summary>
public enum SpeechEngine
{
    /// <summary>Whisper.net — local, well-tested, widest language coverage (~99).</summary>
    Whisper,

    /// <summary>Gemma 4 via llama-server (llama.cpp) sidecar process.</summary>
    Gemma4,

    /// <summary>
    /// NVIDIA Parakeet TDT 0.6B v3 via sherpa-onnx, in-process. CPU-only,
    /// fastest engine; 25 European languages, always auto-detected;
    /// transcribe-only (no translation). Default (ADR-042).
    /// </summary>
    Parakeet,

    /// <summary>
    /// Cloud transcription via the OpenAI transcription REST protocol
    /// (<c>POST {baseUrl}/audio/transcriptions</c>) — works against OpenAI
    /// itself, Groq, or any other OpenAI-compatible host reachable via a
    /// configurable base URL. Opt-in, bring-your-own-key: audio and the API
    /// key leave this machine and go directly to the configured host
    /// (ADR-032, "Local by default. Cloud by choice.").
    /// </summary>
    OpenAiCompatible,

    /// <summary>
    /// Cloud transcription via xAI's Grok Speech-to-Text REST API
    /// (<c>POST {baseUrl}/stt</c>). Opt-in, bring-your-own-key: audio and the
    /// API key leave this machine and go directly to xAI (ADR-032, "Local by
    /// default. Cloud by choice.").
    /// </summary>
    XaiGrok
}
