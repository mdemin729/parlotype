namespace Parlotype.Core.Audio;

/// <summary>Controls how the audio pipeline hands off audio to the speech recognizer.</summary>
public enum PipelineMode
{
    /// <summary>Accumulates audio and sends complete speech segments to Whisper when silence is detected.</summary>
    Batch,

    /// <summary>Sends audio in fixed-size windows continuously to Whisper.</summary>
    Streaming,

    /// <summary>
    /// Treats the whole session as one utterance: silence never ends it, only the
    /// explicit stop does. For push-to-talk, where the key release already says
    /// "I am done", a silence timeout is a guess layered on top of a fact — and a
    /// wrong guess cuts the recording mid-sentence, corrupting the words either
    /// side of the cut and punctuating a fragment (ADR-060).
    /// </summary>
    /// <remarks>
    /// Not unbounded: audio still splits at
    /// <see cref="Speech.SpeechEngineLimits.MaxUtteranceSeconds"/>, on a speech
    /// boundary so the cut lands in a pause.
    /// </remarks>
    SingleUtterance
}
