using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Audio;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Audio;

/// <summary>
/// Orchestrates Microphone → VAD → Whisper pipeline with batch and streaming modes.
/// </summary>
public sealed class AudioPipelineService : IAudioPipeline
{
    private readonly IAudioCaptureService _capture;
    private readonly IVadService _vad;
    private readonly ISpeechRecognizer _recognizer;
    private readonly ILogger<AudioPipelineService> _logger;

    private PipelineMode _mode;
    private readonly List<float> _sampleBuffer = [];
    private readonly ConcurrentQueue<float[]> _processingQueue = new();
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private bool _disposed;

    /// <summary>Window size for streaming mode (3 seconds at 16kHz).</summary>
    private const int StreamingWindowSamples = 16_000 * 3;

    /// <summary>Maximum buffer before forced processing in batch mode (30 seconds at 16kHz).</summary>
    private const int MaxBatchBufferSamples = 16_000 * 30;

    public bool IsRunning { get; private set; }

    public event EventHandler<TranscriptionEventArgs>? TranscriptionAvailable;

    public AudioPipelineService(
        IAudioCaptureService capture,
        IVadService vad,
        ISpeechRecognizer recognizer,
        ILogger<AudioPipelineService> logger)
    {
        _capture = capture;
        _vad = vad;
        _recognizer = recognizer;
        _logger = logger;
    }

    public async Task StartAsync(PipelineMode mode = PipelineMode.Batch, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
            return;

        _mode = mode;

        if (!_recognizer.IsReady)
            await _recognizer.InitializeAsync(cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _capture.DataAvailable += OnAudioDataAvailable;
        _processingTask = Task.Run(() => ProcessQueueAsync(_cts.Token), _cts.Token);

        await _capture.StartAsync(null, cancellationToken);
        IsRunning = true;
        _logger.LogInformation("Pipeline starting in {Mode} mode", _mode);
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        if (!IsRunning)
            return;

        await _capture.StopAsync(cancellationToken);
        _capture.DataAvailable -= OnAudioDataAvailable;

        // Flush remaining buffer
        FlushBuffer();

        if (_cts is not null)
        {
            // Signal processing loop to drain remaining items then exit
            await _cts.CancelAsync();

            if (_processingTask is not null)
            {
                // Wait for queue drain with a generous timeout for Whisper processing
                var completed = await Task.WhenAny(
                    _processingTask,
                    Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));
                if (completed != _processingTask)
                    _logger.LogWarning("Processing queue drain timed out after 30s");
            }

            _cts.Dispose();
            _cts = null;
        }

        IsRunning = false;
        _logger.LogInformation("Pipeline stopped");
    }

    private void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
    {
        var floatSamples = e.Buffer.Span;

        lock (_sampleBuffer)
        {
            foreach (var s in floatSamples)
                _sampleBuffer.Add(s);

            switch (_mode)
            {
                case PipelineMode.Batch:
                    ProcessBatch();
                    break;
                case PipelineMode.Streaming:
                    ProcessStreaming();
                    break;
            }
        }
    }

    private void ProcessBatch()
    {
        // In batch mode, run VAD on the accumulated buffer
        // When silence is detected at the end, send the speech segments to Whisper
        if (_sampleBuffer.Count < 1024)
            return;

        var segments = _vad.DetectSpeech(_sampleBuffer.ToArray());

        if (segments.Count == 0 && _sampleBuffer.Count > MaxBatchBufferSamples)
        {
            // No speech found and buffer is too large, discard
            _sampleBuffer.Clear();
            return;
        }

        // Check if the last segment ends well before the buffer end (silence detected after speech)
        if (segments.Count > 0)
        {
            _logger.LogDebug("VAD detected {Count} speech segments", segments.Count);
            var lastSegment = segments[^1];
            int silenceAfterSpeech = _sampleBuffer.Count - lastSegment.EndSample;

            // At least 500ms of silence after last speech (8000 samples at 16kHz)
            if (silenceAfterSpeech >= 8_000)
            {
                // Extract all speech samples and queue for transcription
                var speechSamples = ExtractSpeechSamples(_sampleBuffer, segments);
                _processingQueue.Enqueue(speechSamples);
                _sampleBuffer.Clear();
            }
        }

        // Force-flush if buffer is too large
        if (_sampleBuffer.Count > MaxBatchBufferSamples)
        {
            var allSamples = _sampleBuffer.ToArray();
            _processingQueue.Enqueue(allSamples);
            _sampleBuffer.Clear();
        }
    }

    private void ProcessStreaming()
    {
        // In streaming mode, send fixed-size windows to Whisper
        while (_sampleBuffer.Count >= StreamingWindowSamples)
        {
            var window = _sampleBuffer.GetRange(0, StreamingWindowSamples).ToArray();
            _sampleBuffer.RemoveRange(0, StreamingWindowSamples);

            // Run VAD to check if there's any speech in this window
            var segments = _vad.DetectSpeech(window);
            if (segments.Count > 0)
            {
                var speechSamples = ExtractSpeechSamples(window, segments);
                _processingQueue.Enqueue(speechSamples);
            }
        }
    }

    private void FlushBuffer()
    {
        lock (_sampleBuffer)
        {
            if (_sampleBuffer.Count < 1024)
            {
                _sampleBuffer.Clear();
                return;
            }

            _logger.LogDebug("Flushing buffer with {Count} samples", _sampleBuffer.Count);
            var segments = _vad.DetectSpeech(_sampleBuffer.ToArray());
            if (segments.Count > 0)
            {
                var speechSamples = ExtractSpeechSamples(_sampleBuffer, segments);
                _processingQueue.Enqueue(speechSamples);
            }

            _sampleBuffer.Clear();
        }
    }

    private async Task ProcessQueueAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (_processingQueue.TryDequeue(out var samples))
            {
                try
                {
                    _logger.LogDebug("Sending {SampleCount} samples ({Duration:F1}s) to speech recognizer",
                        samples.Length, samples.Length / 16_000.0);
                    // Use CancellationToken.None so in-flight transcription completes even during shutdown
                    var result = await _recognizer.TranscribeAsync(samples, CancellationToken.None);

                    if (!string.IsNullOrWhiteSpace(result.Text))
                    {
                        _logger.LogDebug("Transcription result: {Text}", result.Text);
                        TranscriptionAvailable?.Invoke(this, new TranscriptionEventArgs
                        {
                            Result = result
                        });
                    }
                    else
                    {
                        _logger.LogDebug("Transcription returned empty text");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error during transcription");
                }
            }
            else if (cancellationToken.IsCancellationRequested)
            {
                // Queue is empty and we've been asked to stop — exit
                break;
            }
            else
            {
                try
                {
                    await Task.Delay(50, cancellationToken);
                }
                catch (OperationCanceledException)
                {
                    // Cancellation requested — loop back to drain remaining items
                }
            }
        }
    }

    private static float[] ExtractSpeechSamples(List<float> buffer, List<VadSpeechSegment> segments)
    {
        return ExtractSpeechSamples(buffer.ToArray(), segments);
    }

    private static float[] ExtractSpeechSamples(float[] buffer, List<VadSpeechSegment> segments)
    {
        var result = new List<float>();
        foreach (var segment in segments)
        {
            int start = Math.Max(0, segment.StartSample);
            int end = Math.Min(buffer.Length, segment.EndSample);
            if (end > start)
            {
                result.AddRange(buffer[start..end]);
            }
        }
        return result.ToArray();
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (IsRunning)
            await StopAsync();
    }
}
