using Parlotype.Core.Speech;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockSpeechRecognizer : ISpeechRecognizer
{
    public bool IsReady { get; private set; }
    public int InitializeCount { get; private set; }
    public int UnloadCount { get; private set; }

    public Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsReady = true;
        InitializeCount++;
        return Task.CompletedTask;
    }

    public Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
    {
        return Task.FromResult(new TranscriptionResult { Text = "mock" });
    }

    public Task UnloadAsync()
    {
        IsReady = false;
        UnloadCount++;
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        IsReady = false;
        return ValueTask.CompletedTask;
    }
}
