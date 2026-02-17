namespace Parlotype.Core.Audio;

/// <summary>Controls how the audio pipeline hands off audio to the speech recognizer.</summary>
public enum PipelineMode
{
    /// <summary>Accumulates audio and sends complete speech segments to Whisper when silence is detected.</summary>
    Batch,

    /// <summary>Sends audio in fixed-size windows continuously to Whisper.</summary>
    Streaming
}
