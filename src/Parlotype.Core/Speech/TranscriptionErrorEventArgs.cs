namespace Parlotype.Core.Speech;

/// <summary>
/// Raised by the audio pipeline when transcribing a queued utterance fails.
/// The recording keeps running (later utterances may succeed), but subscribers
/// — e.g. the Transcribe window — can inspect <see cref="Exception"/> (notably
/// <see cref="CloudSpeechTranscriptionException"/>) and tell the user why
/// nothing was typed (ADR-043 amendment).
/// </summary>
public sealed class TranscriptionErrorEventArgs : EventArgs
{
    /// <summary>The failure raised by the speech recognizer.</summary>
    public required Exception Exception { get; init; }
}
