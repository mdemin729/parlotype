using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Audio;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class AudioPipelineTests
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

    private sealed class HeadlessModelDownloadService : IModelDownloadService
    {
        private readonly HttpModelDownloadService _http = new(
            new HttpClient { Timeout = TimeSpan.FromHours(1) },
            NullLogger<HttpModelDownloadService>.Instance);

        public bool IsModelCached(WhisperModelType modelType) => _http.IsModelCached(modelType);

        public async Task<string> EnsureModelAsync(WhisperModelType modelType, CancellationToken cancellationToken = default)
        {
            if (!_http.IsModelCached(modelType))
                await _http.DownloadModelAsync(modelType, null, cancellationToken);
            return _http.GetModelPath(modelType);
        }
    }

    /// <summary>
    /// Lightweight VAD fake: reports speech covering all non-zero samples.
    /// Returns a single segment [0..N) where N is the last non-zero sample index + 1.
    /// </summary>
    private sealed class FakeVadService : IVadService
    {
        public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples)
            => DetectSpeech(samples, new VadOptions());

        public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples, VadOptions options)
        {
            // Find the last non-zero sample to determine speech extent
            int lastNonZero = -1;
            for (int i = samples.Length - 1; i >= 0; i--)
            {
                if (samples[i] != 0f)
                {
                    lastNonZero = i;
                    break;
                }
            }

            if (lastNonZero < 0)
                return [];

            // Find first non-zero sample
            int firstNonZero = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                if (samples[i] != 0f)
                {
                    firstNonZero = i;
                    break;
                }
            }

            return [new VadSpeechSegment(firstNonZero, lastNonZero + 1)];
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Lightweight speech recognizer fake: returns a fixed transcription result.
    /// No model download or Whisper integration needed.
    /// </summary>
    private sealed class FakeSpeechRecognizer : ISpeechRecognizer
    {
        public bool IsReady => true;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
            => Task.FromResult(new TranscriptionResult { Text = "fake transcription" });

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Creates a block of non-zero "speech" samples at the given duration.</summary>
    private static float[] CreateSpeechSamples(int durationMs)
    {
        int count = 16_000 * durationMs / 1000;
        var samples = new float[count];
        // Fill with a simple sine wave so VAD sees non-zero audio
        for (int i = 0; i < count; i++)
            samples[i] = MathF.Sin(2 * MathF.PI * 440 * i / 16_000f) * 0.5f;
        return samples;
    }

    /// <summary>Creates a block of zero-valued "silence" samples at the given duration.</summary>
    private static float[] CreateSilenceSamples(int durationMs)
        => new float[16_000 * durationMs / 1000];

    /// <summary>
    /// Helper: starts a pipeline with the given WaitTimeOption, feeds speech + silence,
    /// waits briefly, then returns whether a transcription was produced.
    /// </summary>
    private static async Task<bool> DidPipelineFlushAsync(WaitTimeOption waitTime, int silenceDurationMs)
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new FakeSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.WaitTime, waitTime.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            NullLogger<AudioPipelineService>.Instance);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.Batch);

        // Feed 1 second of speech (enough to exceed VadMinChunkSamples = 8000)
        capture.SimulateAudioData(CreateSpeechSamples(1000));

        // Feed the specified silence duration
        capture.SimulateAudioData(CreateSilenceSamples(silenceDurationMs));

        // Give the processing loop time to pick up and transcribe
        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        await pipeline.StopAsync();

        // If the flushed task completed within the timeout, transcription was triggered
        // by silence detection (not by StopAsync flush)
        return completed == flushed.Task;
    }

    [Theory]
    [InlineData(WaitTimeOption.Instant)]   // 100ms = 1600 samples → clamped to 8000
    [InlineData(WaitTimeOption.VeryShort)] // 200ms = 3200 samples → clamped to 8000
    [InlineData(WaitTimeOption.Short)]     // 300ms = 4800 samples → clamped to 8000
    public async Task SilenceThreshold_BelowMinimum_ClampedTo500ms(WaitTimeOption waitTime)
    {
        // 300ms of silence should NOT trigger flush even for Instant (100ms),
        // because the threshold is clamped to 500ms (VadMinChunkSamples = 8000).
        bool flushedAt300ms = await DidPipelineFlushAsync(waitTime, 300);
        Assert.False(flushedAt300ms,
            $"WaitTimeOption.{waitTime} should NOT flush with only 300ms of silence " +
            "(threshold is clamped to 500ms minimum)");
    }

    [Theory]
    [InlineData(WaitTimeOption.Instant)]   // clamped to 500ms
    [InlineData(WaitTimeOption.VeryShort)] // clamped to 500ms
    [InlineData(WaitTimeOption.Short)]     // clamped to 500ms
    [InlineData(WaitTimeOption.Medium)]    // exactly 500ms (no clamping)
    public async Task SilenceThreshold_AtOrAboveMinimum_FlushesAt500ms(WaitTimeOption waitTime)
    {
        // 600ms of silence (> 500ms threshold) should trigger flush for all sub-500ms options
        // and for Medium (which is exactly 500ms). Extra margin ensures VAD chunk boundary alignment.
        bool flushedAt600ms = await DidPipelineFlushAsync(waitTime, 600);
        Assert.True(flushedAt600ms,
            $"WaitTimeOption.{waitTime} should flush with 600ms of silence " +
            "(threshold is 500ms for all sub-500ms options and Medium)");
    }

    [Fact]
    public async Task SilenceThreshold_InstantAndMedium_BehaveIdentically()
    {
        // Both Instant (100ms, clamped to 500ms) and Medium (500ms, unclamped)
        // should produce the same behavior: no flush at 300ms, flush at 600ms.
        bool instantAt300 = await DidPipelineFlushAsync(WaitTimeOption.Instant, 300);
        bool mediumAt300 = await DidPipelineFlushAsync(WaitTimeOption.Medium, 300);
        bool instantAt600 = await DidPipelineFlushAsync(WaitTimeOption.Instant, 600);
        bool mediumAt600 = await DidPipelineFlushAsync(WaitTimeOption.Medium, 600);

        Assert.Equal(instantAt300, mediumAt300); // Both false
        Assert.Equal(instantAt600, mediumAt600); // Both true
        Assert.False(instantAt300, "Neither should flush at 300ms");
        Assert.True(instantAt600, "Both should flush at 600ms");
    }

    [Fact]
    public async Task SilenceThreshold_Long_NotClamped_FlushesAtConfiguredDuration()
    {
        // WaitTimeOption.Long = 1000ms = 16000 samples — above VadMinChunkSamples,
        // so it should NOT be clamped. 600ms silence should NOT trigger flush.
        bool flushedAt600ms = await DidPipelineFlushAsync(WaitTimeOption.Long, 600);
        Assert.False(flushedAt600ms,
            "WaitTimeOption.Long (1000ms) should NOT flush with only 600ms of silence");

        // But 1100ms should trigger flush.
        bool flushedAt1100ms = await DidPipelineFlushAsync(WaitTimeOption.Long, 1100);
        Assert.True(flushedAt1100ms,
            "WaitTimeOption.Long (1000ms) should flush with 1100ms of silence");
    }

    [Fact]
    public async Task Pipeline_WithVadAndWhisper_ProducesTranscription()
    {
        // Arrange: create a mock capture service that feeds the WAV file
        var capture = new TestAudioCaptureService();
        await using var vad = new SileroVadService(NullLogger<SileroVadService>.Instance);

        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.BaseEn.ToString());
        await using var recognizer = new WhisperSpeechRecognizer(new HeadlessModelDownloadService(), settings, NullLogger<WhisperSpeechRecognizer>.Instance);

        await using var pipeline = new AudioPipelineService(capture, vad, recognizer, settings, NullLogger<AudioPipelineService>.Instance);

        TranscriptionResult? transcription = null;
        var transcriptionReceived = new TaskCompletionSource<TranscriptionResult>();

        pipeline.TranscriptionAvailable += (_, e) =>
        {
            transcription = e.Result;
            transcriptionReceived.TrySetResult(e.Result);
        };

        // Act: start pipeline and feed audio
        await pipeline.StartAsync(PipelineMode.Batch);

        var floatSamples = TestAudioHelper.LoadWavAsFloatSamples("kennedy.wav");
        capture.SimulateAudioData(floatSamples);

        // Add silence to trigger end-of-speech detection
        capture.SimulateAudioData(new float[16_000]); // 1 second silence

        // Wait for transcription (timeout after 60 seconds)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        cts.Token.Register(() => transcriptionReceived.TrySetCanceled());

        var result = await transcriptionReceived.Task;

        await pipeline.StopAsync();

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.True(result.Text.Trim().Length > 5,
            $"Expected meaningful transcription but got: '{result.Text}'");
        Assert.True(result.Text.Contains("I believe"),
            $"Expected transcription to start with but \"I believe\" got: '{result.Text}'");
    }
}

/// <summary>Test double for IAudioCaptureService that allows manual audio injection.</summary>
internal sealed class TestAudioCaptureService : IAudioCaptureService
{
    public bool IsCapturing { get; private set; }

    public event EventHandler<AudioDataEventArgs>? DataAvailable;

    public Task StartAsync(MicrophoneInfo? device = null, CancellationToken cancellationToken = default)
    {
        IsCapturing = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsCapturing = false;
        return Task.CompletedTask;
    }

    /// <summary>Simulates audio data arriving from a microphone.</summary>
    public void SimulateAudioData(float[] samples)
    {
        DataAvailable?.Invoke(this, new AudioDataEventArgs
        {
            Buffer = samples,
            SampleRate = 16_000
        });
    }

    public ValueTask DisposeAsync()
    {
        IsCapturing = false;
        return ValueTask.CompletedTask;
    }
}
