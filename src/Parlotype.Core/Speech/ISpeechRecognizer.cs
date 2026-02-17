namespace Parlotype.Core.Speech;

/// <summary>
/// Converts audio data into text using a speech recognition engine.
/// </summary>
public interface ISpeechRecognizer : IAsyncDisposable
{
    /// <summary>Loads the speech recognition model.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Transcribes the provided PCM audio data into text.</summary>
    Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<byte> pcmData, CancellationToken cancellationToken = default);

    /// <summary>Whether the model has been loaded and is ready.</summary>
    bool IsReady { get; }
}
