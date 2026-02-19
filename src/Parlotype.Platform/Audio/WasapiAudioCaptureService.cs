using Microsoft.Extensions.Logging;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using Parlotype.Core.Audio;

namespace Parlotype.Platform.Audio;

/// <summary>Windows audio capture using WASAPI via NAudio, resampled to 16 kHz mono 16-bit PCM.</summary>
public sealed class WasapiAudioCaptureService : IAudioCaptureService
{
    private static readonly AudioFormat TargetFormat = AudioFormat.Whisper;

    private readonly ILogger<WasapiAudioCaptureService> _logger;
    private WasapiCapture? _capture;
    private BufferedWaveProvider? _bufferedProvider;
    private ISampleProvider? _resampler;
    private bool _disposed;

    public bool IsCapturing { get; private set; }

    public event EventHandler<AudioDataEventArgs>? DataAvailable;

    public WasapiAudioCaptureService(ILogger<WasapiAudioCaptureService> logger)
    {
        _logger = logger;
    }

    public Task StartAsync(MicrophoneInfo? device = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsCapturing)
            return Task.CompletedTask;

        var enumerator = new MMDeviceEnumerator();
        MMDevice mmDevice;

        if (device is not null)
        {
            try
            {
                mmDevice = enumerator.GetDevice(device.Id);
            }
            catch
            {
                _logger.LogWarning("Specified device {DeviceId} unavailable, falling back to default", device.Id);
                // Fallback to default if specified device is unavailable
                mmDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
            }
        }
        else
        {
            mmDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
        }

        _capture = new WasapiCapture(mmDevice);
        _logger.LogInformation("Starting audio capture on device: {DeviceName}", mmDevice.FriendlyName);
        _capture.DataAvailable += OnCaptureDataAvailable;
        _capture.RecordingStopped += OnRecordingStopped;

        // Create a buffered provider that accepts the capture's native format
        _bufferedProvider = new BufferedWaveProvider(_capture.WaveFormat)
        {
            DiscardOnBufferOverflow = true,
            ReadFully = false // Only return actual audio data, don't pad with silence
        };

        // Resample to 16kHz mono
        var sampleProvider = _bufferedProvider.ToSampleProvider();
        _resampler = new WdlResamplingSampleProvider(sampleProvider, TargetFormat.SampleRate);
        if (_resampler.WaveFormat.Channels > 1)
        {
            _resampler = _resampler.ToMono();
        }

        _logger.LogDebug("Capture format: {Format}, resampling to {TargetRate}Hz mono", _capture.WaveFormat, TargetFormat.SampleRate);

        _capture.StartRecording();
        IsCapturing = true;

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsCapturing)
            return Task.CompletedTask;

        _capture?.StopRecording();
        IsCapturing = false;
        _logger.LogInformation("Audio capture stopped");

        return Task.CompletedTask;
    }

    private void OnCaptureDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (e.BytesRecorded == 0 || _bufferedProvider is null || _resampler is null)
            return;

        _bufferedProvider.AddSamples(e.Buffer, 0, e.BytesRecorded);

        // Read resampled float samples and convert to 16-bit PCM bytes
        var floatBuffer = new float[e.BytesRecorded]; // oversize is fine
        int samplesRead = _resampler.Read(floatBuffer, 0, floatBuffer.Length);

        if (samplesRead <= 0)
            return;

        // Convert float samples to 16-bit PCM bytes
        var pcmBytes = new byte[samplesRead * 2];
        for (int i = 0; i < samplesRead; i++)
        {
            var sample = Math.Clamp(floatBuffer[i], -1.0f, 1.0f);
            short shortSample = (short)(sample * short.MaxValue);
            pcmBytes[i * 2] = (byte)(shortSample & 0xFF);
            pcmBytes[i * 2 + 1] = (byte)((shortSample >> 8) & 0xFF);
        }

        DataAvailable?.Invoke(this, new AudioDataEventArgs
        {
            Buffer = pcmBytes,
            Format = TargetFormat
        });
    }

    private void OnRecordingStopped(object? sender, StoppedEventArgs e)
    {
        IsCapturing = false;
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;

        if (_capture is not null)
        {
            _capture.DataAvailable -= OnCaptureDataAvailable;
            _capture.RecordingStopped -= OnRecordingStopped;
            _capture.Dispose();
            _capture = null;
        }

        _bufferedProvider = null;
        _resampler = null;

        return ValueTask.CompletedTask;
    }
}
