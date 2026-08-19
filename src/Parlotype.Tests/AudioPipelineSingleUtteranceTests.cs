using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Audio;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Hold-scoped push-to-talk: <see cref="PipelineMode.SingleUtterance"/> (ADR-060).
/// Silence never ends the utterance — only the explicit stop does, or the engine
/// ceiling, and the ceiling splits on a speech boundary rather than mid-word.
/// </summary>
public class AudioPipelineSingleUtteranceTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly Dictionary<string, object?> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeKeyboardLayoutService : IKeyboardLayoutService
    {
        public KeyboardLayoutInfo? Detect() => null;
    }

    /// <summary>Reports all non-zero audio as one span, like the shipped batch-mode fake.</summary>
    private sealed class SpanVadService : IVadService
    {
        public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples)
            => DetectSpeech(samples, new VadOptions());

        public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples, VadOptions options)
        {
            int first = -1, last = -1;
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i] == 0f)
                    continue;
                if (first < 0)
                    first = i;
                last = i;
            }

            return first < 0 ? [] : [new VadSpeechSegment(first, last + 1)];
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Reports every contiguous run of non-zero samples as its own segment, so the
    /// boundary-split path has more than one segment to choose between.
    /// </summary>
    private sealed class SegmentingVadService : IVadService
    {
        public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples)
            => DetectSpeech(samples, new VadOptions());

        public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples, VadOptions options)
        {
            var segments = new List<VadSpeechSegment>();
            int start = -1;

            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i] != 0f)
                {
                    if (start < 0)
                        start = i;
                }
                else if (start >= 0)
                {
                    segments.Add(new VadSpeechSegment(start, i));
                    start = -1;
                }
            }

            if (start >= 0)
                segments.Add(new VadSpeechSegment(start, samples.Length));

            return segments;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Records how much audio each recognizer call received.</summary>
    private sealed class CountingSpeechRecognizer : ISpeechRecognizer
    {
        private readonly List<int> _lengths = [];
        private readonly Lock _gate = new();

        public bool IsReady => true;

        public IReadOnlyList<int> SampleLengths
        {
            get { lock (_gate) return _lengths.ToArray(); }
        }

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<TranscriptionResult> TranscribeAsync(
            ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
        {
            lock (_gate)
                _lengths.Add(samples.Length);
            return Task.FromResult(new TranscriptionResult { Text = "fake transcription" });
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private const int SampleRate = 16_000;

    private static float[] Speech(int durationMs)
    {
        int count = SampleRate * durationMs / 1000;
        var samples = new float[count];
        for (int i = 0; i < count; i++)
            samples[i] = MathF.Sin(2 * MathF.PI * 440 * i / SampleRate) * 0.5f;
        return samples;
    }

    private static float[] Silence(int durationMs) => new float[SampleRate * durationMs / 1000];

    private static AudioPipelineService Build(
        TestAudioCaptureService capture,
        IVadService vad,
        ISpeechRecognizer recognizer,
        ISettingsService settings)
        => new(capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

    /// <summary>
    /// The point of the mode: silence that would end a batch utterance must not end a
    /// hold, because the key release is about to say so explicitly.
    /// </summary>
    [Fact]
    public async Task DoesNotFlush_OnSilenceExceedingThreshold()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SpanVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.Medium.ToString());

        await using var pipeline = Build(capture, vad, recognizer, settings);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);
        capture.SimulateAudioData(Speech(1000));
        capture.SimulateAudioData(Silence(2000));  // 4x the 500 ms threshold

        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.NotSame(flushed.Task, completed);

        await pipeline.StopAsync();
    }

    /// <summary>Same audio in batch mode does flush — the modes really do differ.</summary>
    [Fact]
    public async Task Batch_StillFlushes_OnTheSameSilence()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SpanVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.Medium.ToString());

        await using var pipeline = Build(capture, vad, recognizer, settings);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.Batch);
        capture.SimulateAudioData(Speech(1000));
        capture.SimulateAudioData(Silence(2000));

        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(5)));
        Assert.Same(flushed.Task, completed);

        await pipeline.StopAsync();
    }

    [Fact]
    public async Task TranscribesTheWholeHold_AsOneUtterance_OnStop()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SpanVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.Medium.ToString());

        await using var pipeline = Build(capture, vad, recognizer, settings);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);
        capture.SimulateAudioData(Speech(1000));
        capture.SimulateAudioData(Silence(2000));
        capture.SimulateAudioData(Speech(1000));
        await pipeline.StopAsync();

        // One decode covering the hold, not one per pause.
        Assert.Single(recognizer.SampleLengths);
    }

    /// <summary>
    /// Past the ceiling the hold must be split, but the cut belongs in a pause: the
    /// trailing speech run stays buffered instead of being sliced through.
    /// </summary>
    [Fact]
    public async Task SplitsAtSpeechBoundary_WhenEngineCeilingExceeded()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SegmentingVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, nameof(SpeechEngine.Parakeet));

        await using var pipeline = Build(capture, vad, recognizer, settings);

        var split = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => split.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);

        // Parakeet's ceiling is 60 s of *speech*; the pause between the runs does not
        // count toward it, so the runs themselves have to exceed it.
        capture.SimulateAudioData(Speech(35_000));
        capture.SimulateAudioData(Silence(2_000));
        capture.SimulateAudioData(Speech(35_000));

        var completed = await Task.WhenAny(split.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.Same(split.Task, completed);

        await pipeline.StopAsync();

        // Two decodes: the completed run at the ceiling, then the retained tail on stop.
        Assert.Equal(2, recognizer.SampleLengths.Count);

        // The first carries only the first run — proof the cut landed in the pause
        // rather than slicing the second run in half.
        Assert.InRange(recognizer.SampleLengths[0], SampleRate * 30, SampleRate * 40);
    }

    [Fact]
    public async Task DiscardsSilenceOnlyAudio_PastCeiling()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SegmentingVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, nameof(SpeechEngine.Parakeet));

        await using var pipeline = Build(capture, vad, recognizer, settings);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);
        capture.SimulateAudioData(Silence(65_000));
        await pipeline.StopAsync();

        Assert.Empty(recognizer.SampleLengths);
    }

    [Theory]
    [InlineData(SpeechEngine.Parakeet, SpeechEngineLimits.ParakeetMaxUtteranceSeconds)]
    [InlineData(SpeechEngine.Whisper, SpeechEngineLimits.WhisperMaxUtteranceSeconds)]
    [InlineData(SpeechEngine.Gemma4, SpeechEngineLimits.UnmeasuredMaxUtteranceSeconds)]
    [InlineData(SpeechEngine.OpenAiCompatible, SpeechEngineLimits.UnmeasuredMaxUtteranceSeconds)]
    [InlineData(SpeechEngine.XaiGrok, SpeechEngineLimits.UnmeasuredMaxUtteranceSeconds)]
    public void EngineCeiling_IsMeasuredWhereMeasured_ConservativeOtherwise(
        SpeechEngine engine, int expectedSeconds)
    {
        Assert.Equal(expectedSeconds, SpeechEngineLimits.MaxUtteranceSeconds(engine));
    }

    /// <summary>
    /// Parakeet's ceiling exists because quality collapses long before the hard
    /// 400 s sherpa-onnx crash point, so it must sit well clear of it.
    /// </summary>
    [Fact]
    public void ParakeetCeiling_StaysWellBelowTheNativeCrashPoint()
    {
        Assert.True(SpeechEngineLimits.ParakeetMaxUtteranceSeconds < 400);
    }
    // --- regression coverage for the ADR-060 code review ---

    /// <summary>
    /// The ceiling governs what the recognizer receives, not how long the key was held.
    /// A hold that is mostly thinking pauses produces little audio, so it must not be
    /// split — splitting it would reintroduce the mid-sentence cut this mode removes.
    /// </summary>
    [Fact]
    public async Task LongSilence_DoesNotTripTheCeiling_WhenSpeechIsUnderIt()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SegmentingVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, nameof(SpeechEngine.Parakeet));

        await using var pipeline = Build(capture, vad, recognizer, settings);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);

        // 85 s of wall-clock, but only 20 s of speech — well under Parakeet's 60 s.
        capture.SimulateAudioData(Speech(10_000));
        capture.SimulateAudioData(Silence(65_000));
        capture.SimulateAudioData(Speech(10_000));

        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(3)));
        Assert.NotSame(flushed.Task, completed);

        await pipeline.StopAsync();

        // One decode on stop, no mid-hold split.
        Assert.Single(recognizer.SampleLengths);
    }

    /// <summary>
    /// Unbroken speech past the ceiling has no boundary to cut on, so the cut lands
    /// mid-speech — but the overrun must be retained, not dropped. VAD only runs per
    /// 8000 new samples, so the unscanned tail is live speech the user is still producing.
    /// </summary>
    [Fact]
    public async Task ContinuousSpeech_PastCeiling_RetainsTheOverrunInsteadOfDroppingIt()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SegmentingVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, nameof(SpeechEngine.Parakeet));

        await using var pipeline = Build(capture, vad, recognizer, settings);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);

        // 75 s of unbroken speech: one segment, no pause anywhere.
        capture.SimulateAudioData(Speech(75_000));

        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.Same(flushed.Task, completed);

        await pipeline.StopAsync();

        // Cut at the 60 s ceiling, and the remaining 15 s came out on stop rather than
        // being thrown away with the buffer.
        Assert.Equal(2, recognizer.SampleLengths.Count);
        Assert.InRange(recognizer.SampleLengths[0], SampleRate * 58, SampleRate * 62);

        var total = recognizer.SampleLengths.Sum();
        Assert.InRange(total, SampleRate * 73, SampleRate * 77);
    }

    /// <summary>
    /// The narrower version of the same loss: a closed segment plus a short unscanned
    /// tail. The tail is under `VadMinChunkSamples`, so VAD has not seen it — clearing
    /// the buffer here silently ate up to ~500 ms of speech.
    /// </summary>
    [Fact]
    public async Task ShortUnscannedTail_SurvivesACeilingSplit()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SegmentingVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, nameof(SpeechEngine.Parakeet));

        await using var pipeline = Build(capture, vad, recognizer, settings);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);

        // 65 s of speech, then a pause, then a 100 ms sliver — smaller than the 500 ms
        // VAD chunk, so it is still unscanned when the ceiling trips.
        capture.SimulateAudioData(Speech(65_000));
        capture.SimulateAudioData(Silence(1_000));
        capture.SimulateAudioData(Speech(100));

        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.Same(flushed.Task, completed);

        await pipeline.StopAsync();

        // The sliver has to reach the recognizer, which means a second decode.
        Assert.Equal(2, recognizer.SampleLengths.Count);
        Assert.True(recognizer.SampleLengths[1] > 0);
    }

    /// <summary>
    /// A hold that never ends — a missed key-up — must not grow the buffer forever just
    /// because the speech in it stays under the ceiling.
    /// </summary>
    [Fact]
    public async Task StuckHold_OfPureSilence_DoesNotBufferForever()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SegmentingVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, nameof(SpeechEngine.Parakeet));

        await using var pipeline = Build(capture, vad, recognizer, settings);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);

        // 11 minutes of silence, past the 10-minute raw backstop.
        for (int i = 0; i < 11; i++)
            capture.SimulateAudioData(Silence(60_000));

        await pipeline.StopAsync();

        Assert.Empty(recognizer.SampleLengths);
    }

    /// <summary>
    /// Batch mode's 30 s overflow used to queue the raw buffer, skipping the VAD
    /// extraction every other path applies — worth ~11 points of word retention in the
    /// ADR-060 benchmarks. The flushed audio must be speech-only, not the raw hold.
    /// </summary>
    [Fact]
    public async Task Batch_OverflowFlush_SendsExtractedSpeech_NotTheRawBuffer()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SegmentingVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        // Very Long, so the silence gaps below never trip the ordinary flush and the
        // buffer really does reach the 30 s overflow.
        await settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.VeryLong.ToString());

        await using var pipeline = Build(capture, vad, recognizer, settings);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.Batch);

        // Short bursts separated by 2.5 s gaps — under the 3 s threshold, so the
        // ordinary silence flush never fires and the buffer really does reach the 30 s
        // overflow, carrying ~10 s of speech inside ~31 s of audio.
        for (int i = 0; i < 9; i++)
        {
            capture.SimulateAudioData(Speech(1_000));
            capture.SimulateAudioData(Silence(2_500));
        }

        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.Same(flushed.Task, completed);

        await pipeline.StopAsync();

        // Speech plus the 160 ms inter-segment joins — nowhere near the 30 s raw buffer
        // the old ToArray() path would have sent.
        Assert.NotEmpty(recognizer.SampleLengths);
        Assert.InRange(recognizer.SampleLengths[0], SampleRate * 6, SampleRate * 14);
    }

    /// <summary>
    /// A run that grows past the ceiling and only then closes is exactly the audio
    /// Parakeet silently truncates, so the ceiling has to bind even though a boundary
    /// now exists at the end of the run.
    /// </summary>
    [Fact]
    public async Task ClosedRun_LongerThanTheCeiling_IsStillCutAtTheCeiling()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SegmentingVadService();
        await using var recognizer = new CountingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, nameof(SpeechEngine.Parakeet));

        await using var pipeline = Build(capture, vad, recognizer, settings);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);

        // 90 s unbroken, then a pause that closes the run.
        capture.SimulateAudioData(Speech(90_000));
        capture.SimulateAudioData(Silence(2_000));

        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(20)));
        Assert.Same(flushed.Task, completed);

        await pipeline.StopAsync();

        Assert.Equal(2, recognizer.SampleLengths.Count);

        // Cut at 60 s, not handed over as one 90 s decode.
        Assert.InRange(recognizer.SampleLengths[0], SampleRate * 58, SampleRate * 62);
        Assert.InRange(recognizer.SampleLengths.Sum(), SampleRate * 88, SampleRate * 92);
    }

}
