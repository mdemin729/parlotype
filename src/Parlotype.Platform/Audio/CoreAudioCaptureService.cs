using System.Buffers;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Audio;
using Parlotype.Platform.MacOs;
using static Parlotype.Platform.Audio.CoreAudioInterop;

namespace Parlotype.Platform.Audio;

/// <summary>
/// macOS audio capture through Audio Queue Services, delivering 16 kHz mono
/// float samples (ADR-077).
/// </summary>
/// <remarks>
/// <para>
/// An input audio queue is asked for the pipeline's format directly, and
/// converts from whatever the device produces (usually 44.1 or 48 kHz, often
/// stereo) inside CoreAudio. That removes the resampler the Windows capture
/// path carries, and with it that path's per-callback allocations.
/// </para>
/// <para>
/// The queue calls back on a thread of its own. Buffers are handed to
/// subscribers as pooled memory and re-enqueued as soon as the event returns,
/// which is the contract <see cref="AudioDataEventArgs.Buffer"/> already
/// documents.
/// </para>
/// </remarks>
[SupportedOSPlatform("macos")]
public sealed class CoreAudioCaptureService : IAudioCaptureService
{
    private static readonly AudioFormat TargetFormat = AudioFormat.Whisper;

    /// <summary>100 ms per buffer: the cadence the Windows capture path delivers at.</summary>
    private const int BufferMilliseconds = 100;

    /// <summary>Three buffers in rotation keeps one being filled while one is being read.</summary>
    private const int BufferCount = 3;

    private readonly ILogger<CoreAudioCaptureService> _logger;
    private readonly Lock _gate = new();

    /// <summary>Kept in a field so the thunk the queue calls is not collected.</summary>
    private readonly AudioQueueInputCallback _callback;

    private nint _queue;
    private volatile bool _running;
    private bool _disposed;

    public bool IsCapturing => _running;

    public event EventHandler<AudioDataEventArgs>? DataAvailable;

    public CoreAudioCaptureService(ILogger<CoreAudioCaptureService> logger)
    {
        _logger = logger;
        _callback = OnInputBuffer;
    }

    public async Task StartAsync(MicrophoneInfo? device = null, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (_running)
            return;

        // Without this, a refused or unanswered prompt does not fail: the queue
        // starts and delivers silence, which reads as "the app heard nothing".
        var granted = await MacOsPrivacy.RequestMicrophoneAsync().WaitAsync(cancellationToken).ConfigureAwait(false);
        if (!granted)
        {
            _logger.LogWarning("Microphone access is not granted ({State})", MacOsPrivacy.MicrophoneState);
            throw new MicrophoneAccessDeniedException();
        }

        // Off the caller's thread: creating the queue can be the process's first
        // CoreAudio call, which waits for as long as macOS keeps a microphone
        // prompt on screen, and the caller is usually the UI thread.
        await Task.Run(
            () =>
            {
                lock (_gate)
                {
                    if (!_running)
                        StartQueue(device);
                }
            },
            cancellationToken).ConfigureAwait(false);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        lock (_gate)
            StopQueue();

        return Task.CompletedTask;
    }

    /// <summary>Must be called with <see cref="_gate"/> held.</summary>
    private void StartQueue(MicrophoneInfo? device)
    {
        var format = new AudioStreamBasicDescription
        {
            SampleRate = TargetFormat.SampleRate,
            FormatId = FormatLinearPcm,
            FormatFlags = FormatFlagsFloatPacked,
            ChannelsPerFrame = 1,
            BitsPerChannel = 32,
            BytesPerFrame = sizeof(float),
            BytesPerPacket = sizeof(float),
            FramesPerPacket = 1,
        };

        Check(AudioQueueNewInput(ref format, _callback, 0, 0, 0, 0, out var queue), "create the input queue");

        try
        {
            if (device is not null && !TrySelectDevice(queue, device))
                _logger.LogWarning("Specified device {DeviceId} unavailable, falling back to default", device.Id);

            var bufferBytes = (uint)(TargetFormat.SampleRate * BufferMilliseconds / 1000 * sizeof(float));
            for (var i = 0; i < BufferCount; i++)
            {
                Check(AudioQueueAllocateBuffer(queue, bufferBytes, out var buffer), "allocate a capture buffer");
                Check(AudioQueueEnqueueBuffer(queue, buffer, 0, 0), "enqueue a capture buffer");
            }

            // Set before the queue starts: the first callback can arrive before
            // AudioQueueStart returns, and it re-enqueues only while running.
            _queue = queue;
            _running = true;

            Check(AudioQueueStart(queue, 0), "start the input queue");
        }
        catch
        {
            _running = false;
            _queue = 0;
            AudioQueueDispose(queue, immediate: true);
            throw;
        }

        _logger.LogInformation(
            "Starting audio capture on device: {DeviceName}", device?.Name ?? "system default input");
    }

    /// <summary>Must be called with <see cref="_gate"/> held.</summary>
    private void StopQueue()
    {
        if (_queue == 0)
            return;

        var queue = _queue;
        _running = false;
        _queue = 0;

        // Synchronous: both calls return only once the queue's thread is done
        // with the callback, so no buffer arrives after this method returns.
        AudioQueueStop(queue, immediate: true);
        AudioQueueDispose(queue, immediate: true);

        _logger.LogInformation("Audio capture stopped");
    }

    private static bool TrySelectDevice(nint queue, MicrophoneInfo device)
    {
        var uid = MacOsNative.CreateString(device.Id);
        try
        {
            return AudioQueueSetProperty(queue, QueuePropertyCurrentDevice, ref uid, (uint)nint.Size) == NoError;
        }
        finally
        {
            MacOsNative.CFRelease(uid);
        }
    }

    private void OnInputBuffer(
        nint userData, nint queue, nint buffer, nint startTime, uint packetDescriptionCount, nint packetDescriptions)
    {
        try
        {
            var byteCount = Marshal.ReadInt32(buffer, BufferDataByteSizeOffset);
            var sampleCount = byteCount / sizeof(float);

            if (sampleCount > 0 && _running)
            {
                // Rented, not allocated; subscribers must copy during the event
                // (see AudioDataEventArgs.Buffer), so it goes straight back.
                var samples = ArrayPool<float>.Shared.Rent(sampleCount);
                try
                {
                    Marshal.Copy(Marshal.ReadIntPtr(buffer, BufferDataPointerOffset), samples, 0, sampleCount);

                    DataAvailable?.Invoke(this, new AudioDataEventArgs
                    {
                        Buffer = samples.AsMemory(0, sampleCount),
                        SampleRate = TargetFormat.SampleRate,
                    });
                }
                finally
                {
                    ArrayPool<float>.Shared.Return(samples);
                }
            }
        }
        catch (Exception ex)
        {
            // An exception must not unwind into CoreAudio's thread.
            _logger.LogError(ex, "Audio capture callback failed");
        }
        finally
        {
            // Hand the buffer back for the next 100 ms. After a stop the queue
            // refuses it, which is the intended end of the rotation.
            if (_running)
                AudioQueueEnqueueBuffer(queue, buffer, 0, 0);
        }
    }

    private static void Check(int status, string action)
    {
        if (status != NoError)
            throw new InvalidOperationException($"Could not {action}: CoreAudio error {DescribeStatus(status)}.");
    }

    public ValueTask DisposeAsync()
    {
        if (_disposed)
            return ValueTask.CompletedTask;

        _disposed = true;

        lock (_gate)
            StopQueue();

        return ValueTask.CompletedTask;
    }
}
