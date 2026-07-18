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
            new StreamingFileDownloader(
                new HttpClient { Timeout = TimeSpan.FromHours(1) },
                NullLogger<StreamingFileDownloader>.Instance),
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
    /// Keyboard-layout fake with a settable detection result (null = unavailable,
    /// the non-Windows / detection-failed path).
    /// </summary>
    private sealed class FakeKeyboardLayoutService : IKeyboardLayoutService
    {
        public KeyboardLayoutInfo? Result { get; set; }
        public KeyboardLayoutInfo? Detect() => Result;
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

    /// <summary>
    /// Speech recognizer spy that tracks <see cref="InitializeAsync(WhisperOptions, CancellationToken)"/> calls
    /// and simulates unload/reload lifecycle.
    /// </summary>
    private sealed class SpySpeechRecognizer : ISpeechRecognizer
    {
        public bool IsReady { get; private set; }
        public List<WhisperOptions> InitCalls { get; } = [];

        public Task InitializeAsync(CancellationToken cancellationToken = default)
        {
            IsReady = true;
            return Task.CompletedTask;
        }

        public Task InitializeAsync(WhisperOptions options, CancellationToken cancellationToken = default)
        {
            if (IsReady && InitCalls.Count > 0 && options == InitCalls[^1])
                return Task.CompletedTask;

            InitCalls.Add(options);
            IsReady = true;
            return Task.CompletedTask;
        }

        public Task UnloadAsync()
        {
            IsReady = false;
            return Task.CompletedTask;
        }

        public Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
            => Task.FromResult(new TranscriptionResult { Text = "spy transcription" });

        public ValueTask DisposeAsync()
        {
            IsReady = false;
            return ValueTask.CompletedTask;
        }
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
            new FakeKeyboardLayoutService(),
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
    [InlineData(WaitTimeOption.Medium)]    // 500ms
    [InlineData(WaitTimeOption.Long)]      // 1000ms
    [InlineData(WaitTimeOption.Extended)]  // 2000ms
    [InlineData(WaitTimeOption.VeryLong)]  // 3000ms
    public async Task SilenceThreshold_DoesNotFlushBeforeConfiguredDuration(WaitTimeOption waitTime)
    {
        // 300ms of silence should NOT trigger flush for any option (minimum is 500ms)
        bool flushedAt300ms = await DidPipelineFlushAsync(waitTime, 300);
        Assert.False(flushedAt300ms,
            $"WaitTimeOption.{waitTime} should NOT flush with only 300ms of silence");
    }

    [Fact]
    public async Task StartAsync_ReinitializesRecognizer_WhenTranslateSettingChanges()
    {
        // Arrange
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        // Act 1: First start — should initialize with default options (translate=false)
        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        Assert.Single(recognizer.InitCalls);
        Assert.False(recognizer.InitCalls[0].TranslateToEnglish);

        // Act 2: Enable translation + target English, then start again
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString());
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "en");
        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        // Assert: recognizer was reinitialized with updated options
        Assert.Equal(2, recognizer.InitCalls.Count);
        Assert.True(recognizer.InitCalls[1].TranslateToEnglish);
    }

    [Fact]
    public async Task StartAsync_DisablesTranslation_WhenModelDoesNotSupportIt()
    {
        // Arrange: translate intent ON, but model is Large v3 Turbo (no translation)
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString());
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "en");
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.LargeV3Turbo.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        // Act
        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        // Assert: effective translate is gated off despite the saved preference
        Assert.Single(recognizer.InitCalls);
        Assert.Equal(WhisperModelType.LargeV3Turbo, recognizer.InitCalls[0].Model);
        Assert.False(recognizer.InitCalls[0].TranslateToEnglish);
    }

    [Fact]
    public async Task StartAsync_KeepsTranslation_WhenModelSupportsIt()
    {
        // Arrange: translate intent ON with a multilingual model
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString());
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "en");
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.Medium.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        // Act
        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        // Assert
        Assert.Single(recognizer.InitCalls);
        Assert.True(recognizer.InitCalls[0].TranslateToEnglish);
    }

    [Fact]
    public async Task StartAsync_SkipsWhisperTranslation_WhenTargetIsNotEnglish()
    {
        // Whisper can only translate *to English*. A non-English target is a valid
        // user intent (honoured by Gemma 4 via the prompt) but must not flip
        // WhisperOptions.TranslateToEnglish — that would produce English output
        // instead of, say, French.
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString());
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "fr");
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.Medium.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        Assert.Single(recognizer.InitCalls);
        Assert.False(recognizer.InitCalls[0].TranslateToEnglish);
    }

    [Fact]
    public async Task StartAsync_SkipsTranslation_WhenTranslationDisabled()
    {
        // Even with target=en, TranslationEnabled=false must suppress translation.
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslationEnabled, false.ToString());
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "en");
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.Medium.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        Assert.Single(recognizer.InitCalls);
        Assert.False(recognizer.InitCalls[0].TranslateToEnglish);
    }

    [Fact]
    public async Task StartAsync_ResolvesKeyboardSource_ToDetectedLayoutLanguage()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, LanguageCatalog.KeyboardLayoutCode);
        var keyboard = new FakeKeyboardLayoutService
        {
            Result = new KeyboardLayoutInfo("ru", "Russian (Russia)"),
        };

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings, keyboard,
            NullLogger<AudioPipelineService>.Instance);

        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        Assert.Single(recognizer.InitCalls);
        Assert.Equal("ru", recognizer.InitCalls[0].Language);
    }

    [Fact]
    public async Task StartAsync_KeyboardSource_FallsBackToAuto_WhenDetectionUnavailable()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, LanguageCatalog.KeyboardLayoutCode);
        var keyboard = new FakeKeyboardLayoutService { Result = null };

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings, keyboard,
            NullLogger<AudioPipelineService>.Instance);

        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        Assert.Single(recognizer.InitCalls);
        Assert.Equal(LanguageCatalog.AutoDetectCode, recognizer.InitCalls[0].Language);
    }

    [Fact]
    public async Task StartAsync_KeyboardSource_FallsBackToAuto_WhenLayoutLanguageNotInWhisperSet()
    {
        // A keyboard layout whose language Whisper doesn't recognise must not be
        // passed through as WhisperOptions.Language.
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, LanguageCatalog.KeyboardLayoutCode);
        var keyboard = new FakeKeyboardLayoutService
        {
            Result = new KeyboardLayoutInfo("kl", "Greenlandic (Greenland)"),
        };

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings, keyboard,
            NullLogger<AudioPipelineService>.Instance);

        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        Assert.Single(recognizer.InitCalls);
        Assert.Equal(LanguageCatalog.AutoDetectCode, recognizer.InitCalls[0].Language);
    }

    [Fact]
    public async Task StartAsync_MigratesLegacyTranslateToEnglish()
    {
        // Existing installations only have the legacy TranslateToEnglish flag set.
        // The pipeline must migrate that to TranslationEnabled + target=en so the
        // intent survives the schema change.
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.TranslateToEnglish, true.ToString());
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.Medium.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        Assert.Single(recognizer.InitCalls);
        Assert.True(recognizer.InitCalls[0].TranslateToEnglish);

        // The new keys are now written.
        Assert.Equal(true.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled));
        Assert.Equal("en", await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage));
    }

    [Fact]
    public async Task StartAsync_SkipsReinitialization_WhenSettingsUnchanged()
    {
        // Arrange
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SpySpeechRecognizer();
        var settings = new FakeSettingsService();

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        // Act: Start twice with no settings change
        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();
        await pipeline.StartAsync(PipelineMode.Batch);
        await pipeline.StopAsync();

        // Assert: only initialized once (second call short-circuits)
        Assert.Single(recognizer.InitCalls);
    }

    [Fact]
    public async Task SilenceThreshold_Medium_FlushesAt500ms()
    {
        // Medium = 500ms, so 600ms of silence should trigger flush
        bool flushedAt600ms = await DidPipelineFlushAsync(WaitTimeOption.Medium, 600);
        Assert.True(flushedAt600ms,
            "WaitTimeOption.Medium (500ms) should flush with 600ms of silence");
    }

    [Fact]
    public async Task SilenceThreshold_Long_FlushesAtConfiguredDuration()
    {
        // WaitTimeOption.Long = 1000ms — 600ms silence should NOT trigger flush.
        bool flushedAt600ms = await DidPipelineFlushAsync(WaitTimeOption.Long, 600);
        Assert.False(flushedAt600ms,
            "WaitTimeOption.Long (1000ms) should NOT flush with only 600ms of silence");

        // But 1100ms should trigger flush.
        bool flushedAt1100ms = await DidPipelineFlushAsync(WaitTimeOption.Long, 1100);
        Assert.True(flushedAt1100ms,
            "WaitTimeOption.Long (1000ms) should flush with 1100ms of silence");
    }

    /// <summary>Captures formatted log output for content assertions.</summary>
    private sealed class CapturingLogger<T> : Microsoft.Extensions.Logging.ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(Microsoft.Extensions.Logging.LogLevel logLevel) => true;

        public void Log<TState>(
            Microsoft.Extensions.Logging.LogLevel logLevel,
            Microsoft.Extensions.Logging.EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
                Messages.Add(formatter(state, exception));
        }
    }

    /// <summary>
    /// Transcribed text is user-private and log files persist on disk: no log
    /// message may ever contain the transcript (security audit 2026-07-11, S1).
    /// </summary>
    [Fact]
    public async Task Pipeline_NeverLogsTranscriptText()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new FakeSpeechRecognizer(); // returns "fake transcription"
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.Medium.ToString());
        var logger = new CapturingLogger<AudioPipelineService>();

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(), logger);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.Batch);
        capture.SimulateAudioData(CreateSpeechSamples(1000));
        capture.SimulateAudioData(CreateSilenceSamples(600));
        await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        await pipeline.StopAsync();

        Assert.True(flushed.Task.IsCompletedSuccessfully, "expected a transcription to flow through the pipeline");
        lock (logger.Messages)
            Assert.DoesNotContain(logger.Messages, m => m.Contains("fake transcription"));
    }

    /// <summary>Recognizer fake that numbers each utterance so tests can assert order.</summary>
    private sealed class SequencingSpeechRecognizer : ISpeechRecognizer
    {
        private int _count;

        public bool IsReady => true;

        public Task InitializeAsync(CancellationToken cancellationToken = default)
            => Task.CompletedTask;

        public Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
            => Task.FromResult(new TranscriptionResult { Text = $"utterance-{Interlocked.Increment(ref _count)}" });

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>
    /// Speech that is still buffered (no trailing silence) when StopAsync is
    /// called must be flushed through VAD and transcribed before StopAsync
    /// returns — the drain-on-stop guarantee of the channel-based pipeline.
    /// </summary>
    [Fact]
    public async Task StopAsync_FlushesBufferedSpeech_BeforeReturning()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new FakeSpeechRecognizer();
        var settings = new FakeSettingsService();
        // Long threshold so silence-based flushing cannot fire first
        await settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.VeryLong.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        var transcriptions = 0;
        pipeline.TranscriptionAvailable += (_, _) => Interlocked.Increment(ref transcriptions);

        await pipeline.StartAsync(PipelineMode.Batch);
        capture.SimulateAudioData(CreateSpeechSamples(1000)); // no trailing silence
        await pipeline.StopAsync();

        Assert.Equal(1, transcriptions);
    }

    /// <summary>
    /// Utterances separated by silence must produce one TranscriptionAvailable
    /// each, in capture order, even though segmentation and transcription run
    /// on separate tasks.
    /// </summary>
    [Fact]
    public async Task Pipeline_MultipleUtterances_TranscribedInOrder()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new SequencingSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.Medium.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        var received = new List<string>();
        var gotOne = new TaskCompletionSource<bool>();
        var gotTwo = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, e) =>
        {
            lock (received)
            {
                received.Add(e.Result.Text);
                if (received.Count == 1) gotOne.TrySetResult(true);
                if (received.Count == 2) gotTwo.TrySetResult(true);
            }
        };

        await pipeline.StartAsync(PipelineMode.Batch);

        // Utterance 1: speech + enough silence to cross the 500 ms threshold.
        capture.SimulateAudioData(CreateSpeechSamples(1000));
        capture.SimulateAudioData(CreateSilenceSamples(600));

        // Wait until the first utterance flushed before feeding the second, so
        // the two are unambiguous distinct utterances.
        var first = await Task.WhenAny(gotOne.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        Assert.True(first == gotOne.Task, "first utterance was not transcribed within 2s");

        capture.SimulateAudioData(CreateSpeechSamples(1000));
        capture.SimulateAudioData(CreateSilenceSamples(600));

        await Task.WhenAny(gotTwo.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        await pipeline.StopAsync();

        Assert.Equal(["utterance-1", "utterance-2"], received);
    }

    /// <summary>
    /// AudioDataEventArgs.Buffer may be pool-backed and reused once the event
    /// returns (WasapiAudioCaptureService rents its callback buffers). The
    /// pipeline must therefore copy samples synchronously inside the handler:
    /// mutating the source array after SimulateAudioData returns must not
    /// affect what the pipeline buffered. FakeVadService keys on non-zero
    /// samples, so a pipeline that (incorrectly) held a reference would see
    /// only zeros and never produce a transcription.
    /// </summary>
    [Fact]
    public async Task Pipeline_CopiesEventBufferSynchronously_SurvivesSourceMutation()
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new FakeVadService();
        await using var recognizer = new FakeSpeechRecognizer();
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.Medium.ToString());

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

        var flushed = new TaskCompletionSource<bool>();
        pipeline.TranscriptionAvailable += (_, _) => flushed.TrySetResult(true);

        await pipeline.StartAsync(PipelineMode.Batch);

        var speech = CreateSpeechSamples(1000);
        capture.SimulateAudioData(speech);
        Array.Clear(speech); // simulate the capture service reusing its pooled buffer

        capture.SimulateAudioData(CreateSilenceSamples(600));

        var completed = await Task.WhenAny(flushed.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        await pipeline.StopAsync();

        Assert.True(completed == flushed.Task,
            "Pipeline must have copied the speech samples during the DataAvailable event; " +
            "clearing the source buffer afterwards should not erase them.");
    }

    [Fact]
    public async Task Pipeline_WithVadAndWhisper_ProducesTranscription()
    {
        // Arrange: create a mock capture service that feeds the WAV file
        var capture = new TestAudioCaptureService();
        await using var vad = new SileroVadService(NullLogger<SileroVadService>.Instance);

        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.BaseEn.ToString());
        // Cpu, not the Auto default: see WhisperSpeechRecognizerTests for why —
        // Whisper.net's Vulkan probe under Auto can hard-crash the native test
        // host on a machine with no real Vulkan loader (e.g. Linux CI runners).
        await settings.SetAsync(SettingsKeys.RuntimePreference, RuntimePreference.Cpu.ToString());
        await using var recognizer = new WhisperSpeechRecognizer(
            new HeadlessModelDownloadService(),
            settings,
            new NoOpNvidiaEnvironmentProvider(),
            new NoOpVulkanEnvironmentProvider(),
            NullLogger<WhisperSpeechRecognizer>.Instance);

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(),
            NullLogger<AudioPipelineService>.Instance);

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
