using System.Buffers;
using System.Runtime.InteropServices;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;

namespace Parlotype.Platform.Audio;

/// <summary>
/// Orchestrates Microphone → VAD → Whisper pipeline with batch and streaming modes.
/// </summary>
/// <remarks>
/// Three single-threaded stages connected by channels (2026-07 rework, findings
/// P6/P7 in plans/2026-07-11-audio-pipeline-perf-security):
/// <list type="number">
/// <item>the capture callback only publishes the audio level and copies the
/// chunk into a pooled buffer — VAD inference no longer runs on the audio
/// thread, where it risked capture-buffer overflows;</item>
/// <item>the segmenter task owns the sample buffer and runs VAD/segmentation
/// with the exact same thresholds and cadence as before;</item>
/// <item>the transcription task awaits utterances (no polling) and raises the
/// pipeline events.</item>
/// </list>
/// Shutdown propagates by completing the channels: StopAsync completes the raw
/// writer, the segmenter drains + flushes and completes the utterance writer,
/// and the transcription loop drains and exits — same drain-on-stop semantics
/// as the previous CancellationToken + polling design.
/// </remarks>
public sealed class AudioPipelineService : IAudioPipeline, IAudioLevelProvider
{
    private readonly IAudioCaptureService _capture;
    private readonly IVadService _vad;
    private readonly ISpeechRecognizer _recognizer;
    private readonly ISettingsService _settings;
    private readonly IKeyboardLayoutService _keyboardLayout;
    private readonly ILogger<AudioPipelineService> _logger;

    private PipelineMode _mode;

    /// <summary>Accumulated 16 kHz samples. Owned by the segmenter task while the
    /// pipeline runs; touched elsewhere only before start / after stop.</summary>
    private readonly List<float> _sampleBuffer = [];

    /// <summary>Capture chunk in a pooled buffer; only the first <see cref="Length"/> floats are valid.</summary>
    private readonly record struct RawChunk(float[] Buffer, int Length);

    private Channel<RawChunk>? _rawChannel;
    private Channel<float[]>? _utteranceChannel;
    private Task? _segmenterTask;
    private Task? _transcriptionTask;
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
        _sampleBuffer.EnsureCapacity(MaxBatchBufferSamples + SampleRate);

        await EnsureModelInitializedAsync(cancellationToken);

        _rawChannel = Channel.CreateUnbounded<RawChunk>(new UnboundedChannelOptions
        {
            SingleReader = true,
        });
        _utteranceChannel = Channel.CreateUnbounded<float[]>(new UnboundedChannelOptions
        {
            SingleReader = true,
            SingleWriter = true,
        });

        _segmenterTask = Task.Run(() => SegmentLoopAsync(_rawChannel.Reader, _utteranceChannel.Writer));
        _transcriptionTask = Task.Run(() => TranscribeLoopAsync(_utteranceChannel.Reader));

        _capture.DataAvailable += OnAudioDataAvailable;
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

        // Completing the raw writer lets the segmenter drain pending chunks, run
        // the final buffer flush, and complete the utterance writer, which in
        // turn lets the transcription loop drain and exit.
        _rawChannel?.Writer.TryComplete();

        if (_segmenterTask is not null && _transcriptionTask is not null)
        {
            // Wait for drain with a generous timeout for Whisper processing
            var pipelineDrained = Task.WhenAll(_segmenterTask, _transcriptionTask);
            var completed = await Task.WhenAny(
                pipelineDrained,
                Task.Delay(TimeSpan.FromSeconds(30), cancellationToken));
            if (completed != pipelineDrained)
                _logger.LogWarning("Pipeline drain timed out after 30s");
        }

        _rawChannel = null;
        _utteranceChannel = null;
        _segmenterTask = null;
        _transcriptionTask = null;

        IsRunning = false;
        _logger.LogInformation("Pipeline stopped");
    }

    private void OnAudioDataAvailable(object? sender, AudioDataEventArgs e)
    {
        var floatSamples = e.Buffer.Span;
        if (floatSamples.Length == 0)
            return;

        // Compute RMS for UI visualisation on the capture thread (cheap)
        PublishAudioLevel(floatSamples);

        var writer = _rawChannel?.Writer;
        if (writer is null)
            return;

        // Copy into a pooled buffer synchronously — the AudioDataEventArgs.Buffer
        // contract allows the capture service to reuse its buffer once this
        // handler returns. All heavier work (VAD, extraction) happens on the
        // segmenter task, keeping the audio callback fast so the capture buffer
        // never overflows behind a slow VAD inference.
        var chunk = ArrayPool<float>.Shared.Rent(floatSamples.Length);
        floatSamples.CopyTo(chunk);

        if (!writer.TryWrite(new RawChunk(chunk, floatSamples.Length)))
            ArrayPool<float>.Shared.Return(chunk); // writer already completed (stop racing a late callback)
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

    /// <summary>
    /// Stage 2: single owner of <see cref="_sampleBuffer"/>. Appends chunks,
    /// runs VAD/segmentation with the same cadence and thresholds as the
    /// pre-rework design, then flushes the remainder once the raw channel
    /// completes and hands the utterance writer its completion.
    /// </summary>
    private async Task SegmentLoopAsync(ChannelReader<RawChunk> reader, ChannelWriter<float[]> utterances)
    {
        try
        {
            await foreach (var chunk in reader.ReadAllAsync())
            {
                try
                {
                    _sampleBuffer.AddRange(chunk.Buffer.AsSpan(0, chunk.Length));

                    switch (_mode)
                    {
                        case PipelineMode.Batch:
                            ProcessBatch(utterances);
                            break;
                        case PipelineMode.Streaming:
                            ProcessStreaming(utterances);
                            break;
                    }
                }
                catch (Exception ex)
                {
                    // A VAD failure must not kill the stage (the loop would stall
                    // silently); drop the poisoned buffer and keep capturing.
                    _logger.LogError(ex, "Segmenter failed on a chunk; discarding buffered audio");
                    ClearBufferState();
                }
                finally
                {
                    ArrayPool<float>.Shared.Return(chunk.Buffer);
                }
            }

            FlushBuffer(utterances);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Segmenter loop terminated unexpectedly");
        }
        finally
        {
            utterances.TryComplete();
        }
    }

    /// <summary>Minimum new samples before running VAD (8000 samples = 500ms at 16kHz).
    /// Chunks smaller than this don't give GetSpeechTimestamps enough context
    /// for its min_silence/speech_pad post-processing.</summary>
    private const int VadMinChunkSamples = 8_000;

    private void ProcessBatch(ChannelWriter<float[]> utterances)
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

        // Silence-after-speech and overflow checks run on every chunk
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
                utterances.TryWrite(speechSamples);
                ClearBufferState();
                return;
            }
        }

        // Force-flush if buffer is too large
        if (_sampleBuffer.Count > MaxBatchBufferSamples)
        {
            var allSamples = _sampleBuffer.ToArray();
            utterances.TryWrite(allSamples);
            ClearBufferState();
        }
    }

    private void ProcessStreaming(ChannelWriter<float[]> utterances)
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
                utterances.TryWrite(speechSamples);
            }
        }
    }

    /// <summary>Final flush when capture ends: VAD over the whole remaining buffer.</summary>
    private void FlushBuffer(ChannelWriter<float[]> utterances)
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
            utterances.TryWrite(speechSamples);
        }

        ClearBufferState();
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

    /// <summary>
    /// Stage 3: transcribes utterances as they arrive (event-driven — the
    /// previous design polled a ConcurrentQueue every 50 ms) and raises the
    /// pipeline events. Exits when the utterance channel completes and drains.
    /// </summary>
    private async Task TranscribeLoopAsync(ChannelReader<float[]> reader)
    {
        await foreach (var samples in reader.ReadAllAsync())
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
