namespace Parlotype.Core.Audio;

/// <summary>
/// Captures audio from the system microphone and provides float sample data for speech recognition.
/// </summary>
public interface IAudioCaptureService : IAsyncDisposable
{
    /// <summary>Starts capturing audio from the specified device, or the default device if null.</summary>
    Task StartAsync(MicrophoneInfo? device = null, CancellationToken cancellationToken = default);

    /// <summary>Stops capturing audio.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Whether audio capture is currently active.</summary>
    bool IsCapturing { get; }

    /// <summary>Raised when a new chunk of audio data is available.</summary>
    event EventHandler<AudioDataEventArgs> DataAvailable;
}

/// <summary>Carries a chunk of captured audio as normalised float samples.</summary>
public sealed class AudioDataEventArgs : EventArgs
{
    /// <summary>Mono float samples in the range [-1, 1].</summary>
    /// <remarks>
    /// May be backed by a pooled buffer that the capture service reuses once the
    /// event returns. Handlers must consume or copy the samples synchronously
    /// inside the event handler and must not hold on to this memory.
    /// </remarks>
    public required ReadOnlyMemory<float> Buffer { get; init; }

    /// <summary>Sample rate of the audio data (e.g. 16 000 Hz).</summary>
    public required int SampleRate { get; init; }
}
