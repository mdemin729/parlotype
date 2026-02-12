using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>Speech recognition using Whisper.net.</summary>
public sealed class WhisperSpeechRecognizer : ISpeechRecognizer
{
    public bool IsReady { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
