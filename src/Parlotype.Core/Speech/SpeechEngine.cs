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
    Gemma4
}
