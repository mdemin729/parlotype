using Parlotype.Core.Audio;

namespace Parlotype.Platform.Audio;

/// <summary>Windows audio capture using WASAPI via NAudio.</summary>
public sealed class WasapiAudioCaptureService : IAudioCaptureService
{
    public bool IsCapturing { get; private set; }

#pragma warning disable CS0067 // Event is never used (stub implementation)
    public event EventHandler<AudioDataEventArgs>? DataAvailable;
#pragma warning restore CS0067

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }

    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }
}
