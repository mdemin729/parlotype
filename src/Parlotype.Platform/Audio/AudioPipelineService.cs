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

    /// <summary>Cancels the recognizer call in flight when the user discards a recording.</summary>
    private CancellationTokenSource? _transcribeCts;

    /// <summary>
    /// Set by <see cref="CancelAsync"/>. Read by the segmenter and transcription
    /// stages as they wind down, which is what turns the ordinary drain into a
    /// discard. Volatile because the three stages run on different threads.
    /// </summary>
    private volatile bool _discarding;

    /// <summary>Serialises model initialisation so a background prewarm and an
    /// interactive start never load the model (or mutate cached settings) concurrently.</summary>
    private readonly SemaphoreSlim _initLock = new(1, 1);

    /// <summary>
    /// Serialises <see cref="StartAsync"/> against <see cref="ShutdownAsync"/>.
    /// Nothing upstream guarantees a stop/cancel and a start never overlap — the
    /// view model's own cancel path deliberately does not wait on an in-flight
    /// start (ADR-039/057) — and without this lock two overlapping calls could
    /// both pass the `IsRunning` gate, or a stale call's cleanup tail could null
    /// out the fields of a session a concurrent <see cref="StartAsync"/> had
    /// already begun. No disposal needed: same reasoning as <see cref="_initLock"/>.
    /// </summary>
    private readonly SemaphoreSlim _lifecycleLock = new(1, 1);

    // Incremental VAD state for batch mode
    private int _vadProcessedUpTo;
    private readonly List<VadSpeechSegment> _accumulatedSegments = [];

    /// <summary>Merge tolerance: segments closer than this are joined (1024 samples ≈ 64ms at 16kHz).</summary>
    private const int SegmentMergeTolerance = 1024;

    /// <summary>Window size for streaming mode (3 seconds at 16kHz).</summary>
    private const int StreamingWindowSamples = 16_000 * 3;

    /// <summary>Maximum buffer before forced processing in batch mode (30 seconds at 16kHz).</summary>
    private const int MaxBatchBufferSamples = 16_000 * 30;

    /// <summary>
    /// Ceiling for <see cref="PipelineMode.SingleUtterance"/>, cached at pipeline start
    /// from the selected engine (ADR-060). Unlike <see cref="MaxBatchBufferSamples"/> this
    /// is not a memory guard: past it Parakeet starts dropping text outright, so exceeding
    /// it silently loses words rather than merely costing time.
    /// </summary>
    /// <remarks>
    /// Measured against <see cref="AccumulatedSpeechSamples"/> — the audio the recognizer
    /// will actually receive — not against the raw buffer. The limits come from decode-input
    /// benchmarks, and <see cref="SpeechSegmentExtractor"/> throws away everything between
    /// the segments, so a hold made mostly of thinking pauses produces far less audio than
    /// it takes wall-clock. Checking the raw buffer would split such a hold for no reason,
    /// reintroducing the mid-sentence cut this mode exists to remove.
    /// </remarks>
    private int _maxUtteranceSamples = SpeechEngineLimits.UnmeasuredMaxUtteranceSeconds * 16_000;

    /// <summary>
    /// Memory backstop for a hold that never ends — a missed key-up, a lost focus event.
    /// Not a quality limit: <see cref="_maxUtteranceSamples"/> governs decode input, and a
    /// hold that is mostly silence stays under it indefinitely while the raw buffer grows
    /// at 64 KB/s. Ten minutes is far past any real dictation hold.
    /// </summary>
    private const int MaxHoldBufferSamples = 16_000 * 600;

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

        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (IsRunning)
                return;

            _mode = mode;
            _discarding = false;

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

            _transcribeCts = new CancellationTokenSource();
            var transcribeToken = _transcribeCts.Token;

            _segmenterTask = Task.Run(() => SegmentLoopAsync(_rawChannel.Reader, _utteranceChannel.Writer));
            _transcriptionTask = Task.Run(() => TranscribeLoopAsync(_utteranceChannel.Reader, transcribeToken));

            _capture.DataAvailable += OnAudioDataAvailable;
            await _capture.StartAsync(null, cancellationToken);
            IsRunning = true;
            _logger.LogInformation("Pipeline starting in {Mode} mode", _mode);
        }
        finally
        {
            _lifecycleLock.Release();
        }
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

            // Deliberately last. SpeechRecognizerFactory re-reads SettingsKeys.SpeechEngine
            // on its own, so this read and that one can straddle a settings change: the
            // engine can be switched while a start is in flight (the settings view model
            // only blocks while IsRecording, which is not yet true during the model load).
            // Reading after the recognizer has resolved makes the only possible
            // disagreement a conservative one — a ceiling lower than this engine could
            // take — instead of handing Parakeet a Whisper-sized 300 s utterance.
            await CacheUtteranceCeilingAsync(cancellationToken);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken = default) =>
        ShutdownAsync(discard: false, cancellationToken);

    /// <inheritdoc />
    public Task CancelAsync(CancellationToken cancellationToken = default) =>
        ShutdownAsync(discard: true, cancellationToken);

    /// <summary>Longest a stop waits for the pipeline to drain — Whisper may still be mid-utterance.</summary>
    private static readonly TimeSpan StopDrainTimeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// A discard cancels the recognizer instead of waiting for it, so the stages
    /// should unwind at once. The wait exists only so a decode already inside
    /// native code (sherpa-onnx cannot be interrupted there) doesn't leave a
    /// detached task writing into fields the next start is about to reuse.
    /// </summary>
    private static readonly TimeSpan CancelDrainTimeout = TimeSpan.FromSeconds(5);

    /// <summary>
    /// Winds the pipeline down. With <paramref name="discard"/> the buffered
    /// audio is thrown away rather than transcribed: the segmenter skips its
    /// final flush and the transcription stage drops whatever is queued instead
    /// of recognizing it.
    /// </summary>
    private async Task ShutdownAsync(bool discard, CancellationToken cancellationToken)
    {
        await _lifecycleLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsRunning)
                return;

            // Both flags must be set before the writer completes, or the segmenter
            // could reach its flush — and the recognizer its next utterance —
            // believing this is an ordinary stop.
            if (discard)
            {
                _discarding = true;
                _transcribeCts?.Cancel();
            }

            await _capture.StopAsync(cancellationToken);
            _capture.DataAvailable -= OnAudioDataAvailable;

            // Completing the raw writer lets the segmenter drain pending chunks, run
            // the final buffer flush, and complete the utterance writer, which in
            // turn lets the transcription loop drain and exit.
            _rawChannel?.Writer.TryComplete();

            var drained = true;
            if (_segmenterTask is not null && _transcriptionTask is not null)
            {
                // Wait for drain with a generous timeout for Whisper processing
                var pipelineDrained = Task.WhenAll(_segmenterTask, _transcriptionTask);
                var timeout = discard ? CancelDrainTimeout : StopDrainTimeout;
                var completed = await Task.WhenAny(
                    pipelineDrained,
                    Task.Delay(timeout, cancellationToken));
                drained = completed == pipelineDrained;
                if (!drained)
                    _logger.LogWarning("Pipeline drain timed out after {Seconds}s", timeout.TotalSeconds);
            }

            _rawChannel = null;
            _utteranceChannel = null;
            _segmenterTask = null;
            _transcriptionTask = null;

            // Only safe to dispose once the loop that observes the token has exited;
            // after a timed-out drain it is still running, and a CTS needs no
            // disposal anyway unless its WaitHandle was used, which it never is.
            if (drained)
                _transcribeCts?.Dispose();
            _transcribeCts = null;

            IsRunning = false;
            _logger.LogInformation(discard ? "Pipeline cancelled, audio discarded" : "Pipeline stopped");
        }
        finally
        {
            _lifecycleLock.Release();
        }
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
                        case PipelineMode.SingleUtterance:
                            ProcessSingleUtterance(utterances);
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

    /// <summary>
    /// Advances VAD over whatever samples arrived since the last pass, merging the
    /// results into <see cref="_accumulatedSegments"/>. Runs only once enough new
    /// audio has piled up: shorter chunks don't give GetSpeechTimestamps the context
    /// its min_silence/speech_pad post-processing needs.
    /// </summary>
    private void RunIncrementalVad()
    {
        int newSamplesCount = _sampleBuffer.Count - _vadProcessedUpTo;
        if (newSamplesCount < VadMinChunkSamples)
            return;

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

    /// <summary>
    /// Hold-scoped segmentation (ADR-060): silence never ends the utterance, because
    /// the caller already knows when it ended — the key came up. The only reason to
    /// split is the engine ceiling, and that split goes on a speech boundary.
    /// </summary>
    private void ProcessSingleUtterance(ChannelWriter<float[]> utterances)
    {
        RunIncrementalVad();

        if (_accumulatedSegments.Count == 0)
        {
            // Nothing but silence, and more of it than any hold needs. There is no
            // speech here to preserve, so drop it rather than buffer it forever.
            if (_sampleBuffer.Count > MaxHoldBufferSamples)
                ClearBufferState();
            return;
        }

        // The ceiling governs what the recognizer receives, not how long the key was
        // held; the raw cap is only there so a hold that never ends cannot grow without
        // bound.
        if (AccumulatedSpeechSamples() <= _maxUtteranceSamples
            && _sampleBuffer.Count <= MaxHoldBufferSamples)
            return;

        FlushAtSpeechBoundary(utterances);
    }

    /// <summary>
    /// Speech samples accumulated so far — what <see cref="SpeechSegmentExtractor"/> would
    /// hand the recognizer, since it copies only the segments and drops the gaps between
    /// them. This is the quantity <see cref="SpeechEngineLimits"/> was measured against.
    /// </summary>
    private int AccumulatedSpeechSamples()
    {
        var total = 0;
        foreach (var segment in _accumulatedSegments)
        {
            var start = Math.Max(0, segment.StartSample);
            var end = Math.Min(_sampleBuffer.Count, segment.EndSample);
            if (end > start)
                total += end - start;
        }

        return total;
    }

    /// <summary>
    /// Splits an over-long hold without cutting through a word. Everything up to the
    /// final speech run is complete and safe to transcribe; the final run may still be
    /// mid-word, so it stays buffered and becomes the head of the next utterance.
    /// </summary>
    private void FlushAtSpeechBoundary(ChannelWriter<float[]> utterances)
    {
        var buffer = CollectionsMarshal.AsSpan(_sampleBuffer);

        if (_accumulatedSegments.Count > 1)
        {
            var completed = _accumulatedSegments.GetRange(0, _accumulatedSegments.Count - 1);
            var keepFrom = _accumulatedSegments[^1].StartSample;

            _logger.LogInformation(
                "Utterance ceiling reached ({Speech:F0}s speech over {Held:F0}s); splitting after {Count} completed segment(s)",
                AccumulatedSpeechSamples() / (double)SampleRate,
                _sampleBuffer.Count / (double)SampleRate, completed.Count);

            utterances.TryWrite(ExtractSpeechSamples(buffer, completed));
            RetainFrom(keepFrom);
            return;
        }

        // A single run, so there is no pause to cut in. Take it up to the ceiling, or to
        // its own end if it closed first — the ceiling has to bind either way, because a
        // run that closed *after* growing past it is exactly the audio the engine
        // silently truncates.
        var only = _accumulatedSegments[0];
        var cutEnd = Math.Min(only.EndSample, only.StartSample + _maxUtteranceSamples);

        if (cutEnd < only.EndSample)
            _logger.LogWarning(
                "Utterance ceiling reached ({Seconds:F0}s speech) with no pause to split on; cutting mid-speech",
                (cutEnd - only.StartSample) / (double)SampleRate);
        else
            _logger.LogInformation(
                "Utterance ceiling reached over {Held:F0}s held; flushing the completed {Seconds:F0}s run",
                _sampleBuffer.Count / (double)SampleRate,
                (cutEnd - only.StartSample) / (double)SampleRate);

        // The overrun stays buffered rather than being discarded: VAD lags the buffer by
        // up to 500 ms, so the tail it has not scanned yet is live speech.
        utterances.TryWrite(ExtractSpeechSamples(
            buffer, [new VadSpeechSegment(only.StartSample, cutEnd)]));
        RetainFrom(cutEnd);
    }

    /// <summary>
    /// Drops the first <paramref name="startSample"/> samples and rebases the VAD state
    /// onto what is left, so the retained tail keeps its segment without a re-scan.
    /// </summary>
    private void RetainFrom(int startSample)
    {
        if (startSample <= 0)
            return;

        if (startSample >= _sampleBuffer.Count)
        {
            ClearBufferState();
            return;
        }

        _sampleBuffer.RemoveRange(0, startSample);
        _vadProcessedUpTo = Math.Clamp(_vadProcessedUpTo - startSample, 0, _sampleBuffer.Count);

        // Rebase whatever straddles or follows the cut; segments wholly before it have
        // just been transcribed. Rebuilt rather than mutated in place because the cut can
        // land inside a segment, before one, or after all of them.
        var retained = new List<VadSpeechSegment>(_accumulatedSegments.Count);
        foreach (var segment in _accumulatedSegments)
        {
            if (segment.EndSample <= startSample)
                continue;

            retained.Add(new VadSpeechSegment(
                Math.Max(0, segment.StartSample - startSample),
                segment.EndSample - startSample));
        }

        _accumulatedSegments.Clear();
        _accumulatedSegments.AddRange(retained);
    }

    private void ProcessBatch(ChannelWriter<float[]> utterances)
    {
        RunIncrementalVad();

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

        // Force-flush if buffer is too large. Extract rather than dumping the raw
        // buffer: every other path hands the recognizer VAD-extracted speech, and the
        // raw dump measured materially worse — 89 % word retention against 100 % at the
        // same length, purely because this branch ran more often (ADR-060).
        if (_sampleBuffer.Count > MaxBatchBufferSamples)
        {
            var speechSamples = ExtractSpeechSamples(
                CollectionsMarshal.AsSpan(_sampleBuffer), _accumulatedSegments);
            if (speechSamples.Length > 0)
                utterances.TryWrite(speechSamples);
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
        // The user discarded this recording — running VAD over the tail only to
        // queue an utterance nobody will transcribe is wasted work.
        if (_discarding || _sampleBuffer.Count < 1024)
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
    private async Task TranscribeLoopAsync(ChannelReader<float[]> reader, CancellationToken cancellationToken)
    {
        await foreach (var samples in reader.ReadAllAsync())
        {
            // Keep reading so the channel drains and the loop terminates, but
            // recognize nothing: the user threw this recording away.
            if (_discarding)
                continue;

            try
            {
                _logger.LogDebug("Sending {SampleCount} samples ({Duration:F1}s) to speech recognizer",
                    samples.Length, samples.Length / 16_000.0);
                // The token fires only on CancelAsync, so an ordinary stop still
                // lets an in-flight transcription complete.
                var result = await _recognizer.TranscribeAsync(samples, cancellationToken);

                // sherpa-onnx cannot observe cancellation mid-decode, so a call
                // already in flight when CancelAsync fired can return normally
                // instead of throwing, long after ShutdownAsync gave up waiting
                // and a brand-new session started. cancellationToken is the one
                // this loop was handed at StartAsync — it stays bound to *this*
                // session's (now-cancelled) source even after a new StartAsync
                // replaces the field, so this check can't be fooled by a restart.
                if (cancellationToken.IsCancellationRequested)
                {
                    _logger.LogDebug("Transcription finished after cancellation; discarding the result");
                    continue;
                }

                if (_textProcessor is not null)
                    result = result with { Text = _textProcessor.Process(result.Text) };

                if (!string.IsNullOrWhiteSpace(result.Text))
                {
                    // Never log the transcript itself — dictated text is user-private
                    // and log files persist on disk (security audit 2026-07-11, S1).
                    _logger.LogDebug("Transcription result: {Length} chars", result.Text.Length);
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                // A discard, not a failure — raising TranscriptionFailed here
                // would put an error dialog in front of a user who just cancelled.
                _logger.LogDebug("Transcription cancelled; discarding the utterance");
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

    private static float[] ExtractSpeechSamples(ReadOnlySpan<float> buffer, IReadOnlyList<VadSpeechSegment> segments)
    {
        return SpeechSegmentExtractor.Extract(buffer, segments);
    }

    /// <summary>
    /// Caches the engine-specific utterance ceiling (ADR-060). Separate from
    /// <see cref="CacheSettingsAsync"/> so it can run *after* the recognizer resolves —
    /// see the call site for why the ordering matters. Engine parsing matches
    /// <c>SpeechRecognizerFactory.GetRecognizerAsync</c> exactly, fallback included.
    /// </summary>
    private async Task CacheUtteranceCeilingAsync(CancellationToken ct)
    {
        var engineStr = await _settings.GetAsync<string>(SettingsKeys.SpeechEngine, ct);
        var engine = Enum.TryParse<SpeechEngine>(engineStr, ignoreCase: true, out var se)
            ? se
            : SpeechEngine.Parakeet;

        var maxUtteranceSeconds = SpeechEngineLimits.MaxUtteranceSeconds(engine);
        _maxUtteranceSamples = maxUtteranceSeconds * SampleRate;
        _logger.LogInformation("Utterance ceiling: {Seconds}s of speech ({Engine})",
            maxUtteranceSeconds, engine);
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
