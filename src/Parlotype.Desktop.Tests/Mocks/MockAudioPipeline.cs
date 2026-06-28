using Parlotype.Core.Audio;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockAudioPipeline : IAudioPipeline
{
    public event EventHandler<TranscriptionEventArgs>? TranscriptionAvailable;

    public bool IsRunning { get; private set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }
    public int PrewarmCount { get; private set; }

    /// <summary>When set, <see cref="StartAsync"/> will throw this exception.</summary>
    public Exception? ThrowOnStart { get; set; }

    /// <summary>When set, <see cref="StartAsync"/> will delay for this duration before completing.</summary>
    public TimeSpan? StartDelay { get; set; }

    public Task PrewarmAsync(CancellationToken cancellationToken = default)
    {
        PrewarmCount++;
        return Task.CompletedTask;
    }

    public async Task StartAsync(PipelineMode mode = PipelineMode.Batch, CancellationToken cancellationToken = default)
    {
        if (ThrowOnStart is not null)
            throw ThrowOnStart;

        if (StartDelay is not null)
            await Task.Delay(StartDelay.Value, cancellationToken);

        IsRunning = true;
        StartCount++;
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
