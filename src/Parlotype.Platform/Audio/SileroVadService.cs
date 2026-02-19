using Microsoft.Extensions.Logging;
using Parlotype.Core.Audio;
using SileroVad;

namespace Parlotype.Platform.Audio;

/// <summary>Voice activity detection using Silero VAD.</summary>
public sealed class SileroVadService : IVadService
{
    private readonly ILogger<SileroVadService> _logger;
    private Vad? _vad;
    private bool _disposed;

    private Vad Vad => _vad ??= new Vad();

    public SileroVadService(ILogger<SileroVadService> logger)
    {
        _logger = logger;
    }

    public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var timestamps = Vad.GetSpeechTimestamps(
            audio: samples,
            threshold: 0.5f,
            min_speech_duration_ms: 50,
            min_silence_duration_ms: 300,
            window_size_samples: 1024,
            speech_pad_ms: 100);

        var segments = timestamps
            .Select(t => new VadSpeechSegment(t.Start, t.End))
            .ToList();

        _logger.LogDebug("VAD processing {SampleCount} samples, detected {SegmentCount} segments", samples.Length, segments.Count);

        return segments;
    }

    public ValueTask DisposeAsync()
    {
        if (!_disposed)
        {
            _disposed = true;
            _vad?.Dispose();
            _vad = null;
        }

        return ValueTask.CompletedTask;
    }
}
