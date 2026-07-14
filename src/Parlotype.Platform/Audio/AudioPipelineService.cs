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
public sealed class AudioPipelineService : IAudioPipeline, IAudioLevelProvider
{
    private readonly IAudioCaptureService _capture;
    private readonly IVadService _vad;
    private readonly ISpeechRecognizer _recognizer;
    private readonly ISettingsService _settings;
    private readonly IKeyboardLayoutService _keyboardLayout;
    private readonly ILogger<AudioPipelineService> _logger;

    private PipelineMode _mode;
    private readonly List<float> _sampleBuffer = [];
    private readonly ConcurrentQueue<float[]> _processingQueue = new();
    private CancellationTokenSource? _cts;
    private Task? _processingTask;
    private bool _disposed;

    /// <summary>Serialises model initialisation so a background prewarm and an
    /// interactive start never load the model (or mutate cached settings) concurrently.</summary>
    private readonly SemaphoreSlim _initLock = new(1, 1);

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

    /// <summary>Whisper options built from settings, cached at pipeline start.</summary>
    private WhisperOptions? _whisperOptions;

    public bool IsRunning { get; private set; }

    public event EventHandler<TranscriptionEventArgs>? TranscriptionAvailable;
    public event EventHandler<TranscriptionErrorEventArgs>? TranscriptionFailed;

    /// <inheritdoc />
    public float CurrentLevel { get; private set; }

    /// <inheritdoc />
    public event EventHandler<AudioLevelEventArgs>? LevelChanged;

    public AudioPipelineService(
        IAudioCaptureService capture,
        IVadService vad,
        ISpeechRecognizer recognizer,
        ISettingsService settings,
        IKeyboardLayoutService keyboardLayout,
        ILogger<AudioPipelineService> logger)
    {
        _capture = capture;
        _vad = vad;
        _recognizer = recognizer;
        _settings = settings;
        _keyboardLayout = keyboardLayout;
        _logger = logger;
    }

    public async Task PrewarmAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
            return;

        await EnsureModelInitializedAsync(cancellationToken);
        _logger.LogInformation("Pipeline prewarmed: speech model ready");
    }

    public async Task StartAsync(PipelineMode mode = PipelineMode.Batch, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsRunning)
            return;

        _mode = mode;

        // Pre-size once (Clear keeps capacity): avoids staged growth re-allocations
        // of the up-to-1.92 MB backing array on the capture path. Slack covers the
        // chunk that lands just before the overflow check trips.
        lock (_sampleBuffer)
            _sampleBuffer.EnsureCapacity(MaxBatchBufferSamples + SampleRate);

        await EnsureModelInitializedAsync(cancellationToken);

        _cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _capture.DataAvailable += OnAudioDataAvailable;
        _processingTask = Task.Run(() => ProcessQueueAsync(_cts.Token), _cts.Token);

        await _capture.StartAsync(null, cancellationToken);
        IsRunning = true;
        _logger.LogInformation("Pipeline starting in {Mode} mode", _mode);
    }

    /// <summary>
    /// Snapshots settings and loads the speech model under <see cref="_initLock"/>.
    /// Idempotent when the recognizer is already loaded with matching options, so a
    /// prewarm followed by an interactive start performs the heavy load only once.
    /// </summary>
    private async Task EnsureModelInitializedAsync(CancellationToken cancellationToken)
    {
        await _initLock.WaitAsync(cancellationToken);
        try
        {
            // Snapshot settings for this recording session
            await CacheSettingsAsync(cancellationToken);

            if (_whisperOptions is not null)
                await _recognizer.InitializeAsync(_whisperOptions, cancellationToken);
            else if (!_recognizer.IsReady)
                await _recognizer.InitializeAsync(cancellationToken);
        }
        finally
        {
            _initLock.Release();
        }
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

        // Compute RMS for UI visualisation (outside lock — read-only on the span)
        PublishAudioLevel(floatSamples);

        lock (_sampleBuffer)
        {
            // Bulk span append (single vectorised copy). Also the synchronous copy
            // the AudioDataEventArgs.Buffer contract requires — the capture service
            // reuses its pooled buffer after this handler returns.
            _sampleBuffer.AddRange(floatSamples);

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

    private void PublishAudioLevel(ReadOnlySpan<float> samples)
    {
        if (samples.Length == 0)
            return;

        double sum = 0;
        foreach (var s in samples)
            sum += s * (double)s;

        var rms = (float)Math.Sqrt(sum / samples.Length);
        // Clamp to [0, 1] — microphone samples are normalised but RMS can briefly exceed 1.0
        rms = Math.Min(rms, 1f);

        CurrentLevel = rms;
        // Capture-thread hot path: skip the event-args allocation when nobody listens
        if (LevelChanged is { } levelChanged)
            levelChanged(this, new AudioLevelEventArgs { Level = rms });
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
            // Single copy straight from the backing array (GetRange + ToArray copied twice)
            var window = CollectionsMarshal.AsSpan(_sampleBuffer)[..StreamingWindowSamples].ToArray();
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
                    // The pipeline keeps running (later utterances may succeed);
                    // subscribers surface the failure to the user (ADR-043 amendment).
                    TranscriptionFailed?.Invoke(this, new TranscriptionErrorEventArgs { Exception = ex });
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
        // One-shot migration from the pre-redesign settings (legacy TranslateToEnglish
        // flag, shared RecentLanguages MRU) to TranslationEnabled + per-role MRUs.
        // Idempotent — returns early once migration has run.
        await LanguageSettingsMigrator.MigrateAsync(_settings, ct);

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

        // Build WhisperOptions from settings for recognizer initialization
        var savedModel = await _settings.GetAsync<string>(SettingsKeys.SelectedWhisperModel, ct);
        var modelType = Enum.TryParse<WhisperModelType>(savedModel, out var mt) ? mt : WhisperModelType.Base;

        var translationEnabledStr = await _settings.GetAsync<string>(SettingsKeys.TranslationEnabled, ct);
        var translationEnabled = bool.TryParse(translationEnabledStr, out var te) && te;

        var targetCode = await _settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage, ct);

        // Whisper can only translate *to English*. Other targets are valid intents but
        // are ignored at the Whisper layer (the Gemma 4 engine honours them via the
        // prompt). Model capability (ADR-033) gates the actual flag regardless.
        var translateToEnglish = translationEnabled
            && string.Equals(targetCode, LanguageCatalog.EnglishCode, StringComparison.OrdinalIgnoreCase);
        var effectiveTranslate = translateToEnglish && WhisperModelInfo.Get(modelType).SupportsTranslation;

        var runtimeStr = await _settings.GetAsync<string>(SettingsKeys.RuntimePreference, ct);
        var runtime = Enum.TryParse<RuntimePreference>(runtimeStr, out var rp) ? rp : RuntimePreference.Auto;

        // Source language: "auto" (detection), "keyboard" (OS layout, resolved
        // here), or an explicit Whisper language code. The keyboard sentinel
        // resolves to the detected layout language, falling back to auto when
        // detection is unavailable or the layout language isn't one Whisper knows.
        var sourceLang = await _settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, ct);
        var detectedLayout = LanguageCatalog.IsKeyboardLayout(sourceLang) ? _keyboardLayout.Detect() : null;
        var language = SourceLanguageResolver.Resolve(sourceLang, detectedLayout, LanguageCatalog.WhisperLanguages);
        if (LanguageCatalog.IsKeyboardLayout(sourceLang))
            _logger.LogInformation("Keyboard-layout source resolved: {Layout} → {Language}",
                detectedLayout?.FriendlyName ?? "(undetected)", language);

        _whisperOptions = new WhisperOptions
        {
            Model = modelType,
            Language = language,
            TranslateToEnglish = effectiveTranslate,
            RuntimePreference = runtime,
        };

        _logger.LogInformation(
            "Whisper options: Model={Model}, Language={Language}, Translate={Translate}, Runtime={Runtime}",
            _whisperOptions.Model, _whisperOptions.Language, _whisperOptions.TranslateToEnglish, _whisperOptions.RuntimePreference);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (IsRunning)
            await StopAsync();

        // A fire-and-forget background prewarm may still hold _initLock during a
        // multi-second cold load. Drain it before disposing so its finally-block
        // Release() doesn't run against a disposed semaphore. If it can't be
        // acquired quickly (a slow load), skip Dispose — a SemaphoreSlim needs no
        // disposal unless its AvailableWaitHandle was used, which it never is here.
        if (await _initLock.WaitAsync(TimeSpan.FromSeconds(5)))
            _initLock.Dispose();
    }
}
