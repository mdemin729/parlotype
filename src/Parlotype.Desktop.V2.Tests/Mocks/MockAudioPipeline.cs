using Parlotype.Core.Audio;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.V2.Tests.Mocks;

public sealed class MockAudioPipeline : IAudioPipeline
{
    public event EventHandler<TranscriptionEventArgs>? TranscriptionAvailable;

    public bool IsRunning { get; private set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }

    /// <summary>When set, <see cref="StartAsync"/> will throw this exception.</summary>
    public Exception? ThrowOnStart { get; set; }

    public Task StartAsync(PipelineMode mode = PipelineMode.Batch, CancellationToken cancellationToken = default)
    {
        if (ThrowOnStart is not null)
            throw ThrowOnStart;

        IsRunning = true;
        StartCount++;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = false;
        StopCount++;
        return Task.CompletedTask;
    }

    public void RaiseTranscriptionAvailable(string text) =>
        TranscriptionAvailable?.Invoke(this, new TranscriptionEventArgs
        {
            Result = new TranscriptionResult { Text = text }
        });

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
