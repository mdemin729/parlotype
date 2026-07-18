using System.Buffers;
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
    private int _nativeBlockAlign = 1;
    private int _nativeSampleRate = AudioFormat.Whisper.SampleRate;
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

        // Cached for the per-callback resampled-output estimate
        _nativeBlockAlign = Math.Max(1, _capture.WaveFormat.BlockAlign);
        _nativeSampleRate = Math.Max(1, _capture.WaveFormat.SampleRate);

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

        // Request only ~2× the expected 16 kHz mono output for this callback.
        // The NAudio resampler chain allocates internally in proportion to the
        // REQUESTED count on every Read — asking for BytesRecorded floats cost
        // ~2.7 MB per 100 ms callback (~27 MB/s while recording), and the pool's
        // bucket rounding made it worse; a right-sized request costs ~0.3 MB
        // (see memory/knowledge/naudio-resampler-read-cost.md). The estimate is
        // deterministic (input frames × rate ratio), so the 2× slack means a
        // single read consumes everything the resampler has; any filter-latency
        // remainder simply arrives with the next callback ~10 ms later.
        int nativeFrames = e.BytesRecorded / _nativeBlockAlign;
        int expectedSamples = (int)((long)nativeFrames * TargetFormat.SampleRate / _nativeSampleRate);
        int requestSamples = Math.Max(expectedSamples * 2, 1024);

        // Rented, not allocated; subscribers must copy during the event
        // (see AudioDataEventArgs.Buffer), so the buffer is returned right after.
        var floatBuffer = ArrayPool<float>.Shared.Rent(requestSamples);
        try
        {
            // Pass requestSamples, not floatBuffer.Length — the pool rounds the
            // buffer up to a power of two, and a bigger request directly
            // multiplies the resampler's internal allocations.
            int samplesRead = _resampler.Read(floatBuffer, 0, requestSamples);

            if (samplesRead <= 0)
                return;

            DataAvailable?.Invoke(this, new AudioDataEventArgs
            {
                Buffer = floatBuffer.AsMemory(0, samplesRead),
                SampleRate = TargetFormat.SampleRate
            });
        }
        finally
        {
            ArrayPool<float>.Shared.Return(floatBuffer);
        }
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
