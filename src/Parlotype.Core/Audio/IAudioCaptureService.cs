namespace Parlotype.Core.Audio;

/// <summary>
/// Captures audio from the system microphone and provides PCM data for speech recognition.
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

/// <summary>Carries a chunk of captured PCM audio data.</summary>
public sealed class AudioDataEventArgs : EventArgs
{
    public required ReadOnlyMemory<byte> Buffer { get; init; }
    public required AudioFormat Format { get; init; }
}
