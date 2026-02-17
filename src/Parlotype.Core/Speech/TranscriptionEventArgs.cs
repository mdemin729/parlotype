namespace Parlotype.Core.Speech;

/// <summary>Carries a transcription result from the audio pipeline.</summary>
public sealed class TranscriptionEventArgs : EventArgs
{
    public required TranscriptionResult Result { get; init; }
}
