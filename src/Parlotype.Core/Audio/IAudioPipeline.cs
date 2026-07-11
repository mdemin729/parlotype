using Parlotype.Core.Speech;

namespace Parlotype.Core.Audio;

/// <summary>
/// Orchestrates the full audio pipeline: Microphone → VAD → Speech Recognition → Transcription.
/// </summary>
public interface IAudioPipeline : IAsyncDisposable
{
    /// <summary>
    /// Loads the speech model ahead of time so the first <see cref="StartAsync"/>
    /// call is instant rather than blocking on a cold model load. Best-effort and
    /// safe to call multiple times; does not start audio capture. No-op by default.
    /// </summary>
    Task PrewarmAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

    /// <summary>Starts the audio pipeline with the specified mode.</summary>
    Task StartAsync(PipelineMode mode = PipelineMode.Batch, CancellationToken cancellationToken = default);

    /// <summary>Stops the audio pipeline.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the pipeline is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Raised when a transcription result is available.</summary>
    event EventHandler<TranscriptionEventArgs> TranscriptionAvailable;

    /// <summary>
    /// Raised when transcribing a queued utterance fails. The pipeline keeps
    /// running; subscribers can surface the error to the user (ADR-043 amendment).
    /// </summary>
    event EventHandler<TranscriptionErrorEventArgs> TranscriptionFailed;
}
