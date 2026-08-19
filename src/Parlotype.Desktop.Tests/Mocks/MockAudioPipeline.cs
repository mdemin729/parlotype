using Parlotype.Core.Audio;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockAudioPipeline : IAudioPipeline
{
    public event EventHandler<TranscriptionEventArgs>? TranscriptionAvailable;
    public event EventHandler<TranscriptionErrorEventArgs>? TranscriptionFailed;

    public bool IsRunning { get; private set; }
    public int StartCount { get; private set; }
    public int StopCount { get; private set; }
    public int CancelCount { get; private set; }
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

    /// <summary>Mode the last <see cref="StartAsync"/> ran with (ADR-060 plumbing).</summary>
    public PipelineMode? LastStartMode { get; private set; }

    public async Task StartAsync(PipelineMode mode = PipelineMode.Batch, CancellationToken cancellationToken = default)
    {
        LastStartMode = mode;

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

    public Task CancelAsync(CancellationToken cancellationToken = default)
    {
        IsRunning = false;
        CancelCount++;
        return Task.CompletedTask;
    }

    public void RaiseTranscriptionAvailable(string text) =>
        TranscriptionAvailable?.Invoke(this, new TranscriptionEventArgs
        {
            Result = new TranscriptionResult { Text = text }
        });

    public void RaiseTranscriptionFailed(Exception exception) =>
        TranscriptionFailed?.Invoke(this, new TranscriptionErrorEventArgs { Exception = exception });

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
