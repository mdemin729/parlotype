using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;

namespace Parlotype.Platform.Audio;

/// <summary>
/// Orchestrates Microphone → VAD → Whisper pipeline with batch and streaming modes.
/// </summary>
public sealed class AudioPipelineService : IAudioPipeline
{
    private readonly IAudioCaptureService _capture;
    private readonly IVadService _vad;
    private readonly ISpeechRecognizer _recognizer;
    private readonly ISettingsService _settings;
    private readonly ILogger<AudioPipelineService> _logger;

    private PipelineMode _mode;
    private readonly List<float> _sampleBuffer = [];
    private readonly ConcurrentQueue<float[]> _processingQueue = new();
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private bool _disposed;

    // Incremental VAD state for batch mode
    private int _vadProcessedUpTo;
    private readonly List<VadSpeechSegment> _accumulatedSegments = [];

    /// <summary>Merge tolerance: segments closer than this are joined (1024 samples ≈ 64ms at 16kHz).</summary>
    private const int SegmentMergeTolerance = 1024;

    /// <summary>Window size for streaming mode (3 seconds at 16kHz).</summary>
    private const int StreamingWindowSamples = 16_000 * 3;

    /// <summary>Maximum buffer before forced processing in batch mode (30 seconds at 16kHz).</summary>
    private const int MaxBatchBufferSamples = 16_000 * 30;

    private const int SampleRate = 16_000;

    /// <summary>Silence threshold in samples, cached at pipeline start from settings.</summary>
    private int _silenceThresholdSamples = SampleRate / 2; // default 500ms

    /// <summary>Post-processor for transcription text, built at pipeline start from settings.</summary>
    private TranscriptionTextProcessor? _textProcessor;

    public bool IsRunning { get; private set; }

    public event EventHandler<TranscriptionEventArgs>? TranscriptionAvailable;

    public AudioPipelineService(
        IAudioCaptureService capture,
        IVadService vad,
        ISpeechRecognizer recognizer,
        ISettingsService settings,
        ILogger<AudioPipelineService> logger)
    {
        _capture = capture;
        _vad = vad;
        _recognizer = recognizer;
        _settings = settings;
        _logger = logger;
    }

    public async Task StartAsync(PipelineMode mode = PipelineMode.Batch, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
            return;

        _mode = mode;

        // Snapshot settings for this recording session
        await CacheSettingsAsync(cancellationToken);

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

    /// <summary>Minimum new samples before running VAD (8000 samples = 500ms at 16kHz).
    /// Chunks smaller than this don't give GetSpeechTimestamps enough context
    /// for its min_silence/speech_pad post-processing.</summary>
    private const int VadMinChunkSamples = 8_000;

    private void ProcessBatch()
    {
        // Run VAD only when enough new samples have accumulated
        int newSamplesCount = _sampleBuffer.Count - _vadProcessedUpTo;
        if (newSamplesCount >= VadMinChunkSamples)
        {
            var bufferSpan = CollectionsMarshal.AsSpan(_sampleBuffer);
            var newSegments = _vad.DetectSpeech(bufferSpan[_vadProcessedUpTo..]);

            foreach (var seg in newSegments)
            {
                var adjusted = new VadSpeechSegment(
                    seg.StartSample + _vadProcessedUpTo,
                    seg.EndSample + _vadProcessedUpTo);
                MergeOrAddSegment(_accumulatedSegments, adjusted);
            }

            _vadProcessedUpTo = _sampleBuffer.Count;
        }

        // Silence-after-speech and overflow checks run on every callback
        // so end-of-utterance is detected promptly.

        if (_accumulatedSegments.Count == 0 && _sampleBuffer.Count > MaxBatchBufferSamples)
        {
            // No speech found and buffer is too large, discard
            ClearBufferState();
            return;
        }

        // Check if the last segment ends well before the buffer end (silence detected after speech)
        if (_accumulatedSegments.Count > 0)
        {
            _logger.LogDebug("VAD detected {Count} speech segments", _accumulatedSegments.Count);
            var lastSegment = _accumulatedSegments[^1];
            int silenceAfterSpeech = _sampleBuffer.Count - lastSegment.EndSample;

            // Silence after last speech exceeds the configured threshold
            if (silenceAfterSpeech >= _silenceThresholdSamples)
            {
                // Extract all speech samples and queue for transcription
                var speechSamples = ExtractSpeechSamples(
                    CollectionsMarshal.AsSpan(_sampleBuffer), _accumulatedSegments);
                _processingQueue.Enqueue(speechSamples);
                ClearBufferState();
                return;
            }
        }

        // Force-flush if buffer is too large
        if (_sampleBuffer.Count > MaxBatchBufferSamples)
        {
            var allSamples = _sampleBuffer.ToArray();
            _processingQueue.Enqueue(allSamples);
            ClearBufferState();
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
                ClearBufferState();
                return;
            }

            _logger.LogDebug("Flushing buffer with {Count} samples", _sampleBuffer.Count);

            // Final flush: run VAD on the entire buffer for complete detection.
            // This runs once at shutdown so O(n) cost is acceptable.
            var segments = _vad.DetectSpeech(CollectionsMarshal.AsSpan(_sampleBuffer));
            if (segments.Count > 0)
            {
                var speechSamples = ExtractSpeechSamples(
                    CollectionsMarshal.AsSpan(_sampleBuffer), segments);
                _processingQueue.Enqueue(speechSamples);
            }

            ClearBufferState();
        }
    }

    /// <summary>Resets sample buffer and incremental VAD state.</summary>
    private void ClearBufferState()
    {
        _sampleBuffer.Clear();
        _vadProcessedUpTo = 0;
        _accumulatedSegments.Clear();
    }

    /// <summary>
    /// Merges <paramref name="segment"/> with the last accumulated segment if they are
    /// adjacent or overlapping (gap ≤ <see cref="SegmentMergeTolerance"/>), otherwise appends.
    /// </summary>
    private static void MergeOrAddSegment(List<VadSpeechSegment> segments, VadSpeechSegment segment)
    {
        if (segments.Count > 0)
        {
            var last = segments[^1];
            if (segment.StartSample <= last.EndSample + SegmentMergeTolerance)
            {
                segments[^1] = new VadSpeechSegment(
                    last.StartSample,
                    Math.Max(last.EndSample, segment.EndSample));
                return;
            }
        }

        segments.Add(segment);
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

                    if (_textProcessor is not null)
                        result = result with { Text = _textProcessor.Process(result.Text) };

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

    private static float[] ExtractSpeechSamples(ReadOnlySpan<float> buffer, List<VadSpeechSegment> segments)
    {
        return SpeechSegmentExtractor.Extract(buffer, segments);
    }

    private async Task CacheSettingsAsync(CancellationToken ct)
    {
        var savedWaitTime = await _settings.GetAsync<string>(SettingsKeys.WaitTime, ct);
        var waitTime = Enum.TryParse<WaitTimeOption>(savedWaitTime, out var wt) ? wt : WaitTimeOption.Medium;
        _silenceThresholdSamples = (int)(waitTime.GetSeconds() * SampleRate);
        _logger.LogInformation("Silence threshold: {WaitTime} ({Ms}ms)",
            waitTime, waitTime.GetSeconds() * 1000);

        var punctuationStr = await _settings.GetAsync<string>(SettingsKeys.AutomaticPunctuation, ct);
        var punctuationEnabled = !bool.TryParse(punctuationStr, out var p) || p; // default true

        var profanityStr = await _settings.GetAsync<string>(SettingsKeys.FilterProfanity, ct);
        var profanityEnabled = bool.TryParse(profanityStr, out var f) && f; // default false

        var needsProcessor = !punctuationEnabled || profanityEnabled;
        _textProcessor = needsProcessor
            ? new TranscriptionTextProcessor(stripPunctuation: !punctuationEnabled, filterProfanity: profanityEnabled)
            : null;
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
