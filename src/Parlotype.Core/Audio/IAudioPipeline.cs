using Parlotype.Core.Speech;

namespace Parlotype.Core.Audio;

/// <summary>
/// Orchestrates the full audio pipeline: Microphone → VAD → Speech Recognition → Transcription.
/// </summary>
public interface IAudioPipeline : IAsyncDisposable
{
    /// <summary>Starts the audio pipeline with the specified mode.</summary>
    Task StartAsync(PipelineMode mode = PipelineMode.Batch, CancellationToken cancellationToken = default);

    /// <summary>Stops the audio pipeline.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether the pipeline is currently running.</summary>
    bool IsRunning { get; }

    /// <summary>Raised when a transcription result is available.</summary>
    event EventHandler<TranscriptionEventArgs> TranscriptionAvailable;
}
