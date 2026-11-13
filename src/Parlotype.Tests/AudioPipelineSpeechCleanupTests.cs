using Microsoft.Extensions.Logging;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextProcessing;
using Parlotype.Platform.Audio;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Speech cleanup inside the pipeline (ADR-073): the recognized text is
/// rewritten when the feature is on, and in every other case, including every
/// way cleanup can fail, the raw text reaches the user unchanged.
/// </summary>
public sealed class AudioPipelineSpeechCleanupTests
{
    private const string RawText = "so um i need to send the report by uh friday no wait thursday";
    private const string CleanText = "I need to send the report by Thursday.";

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

    private sealed class FakeMicrophoneEnumerator : IMicrophoneEnumerator
    {
#pragma warning disable CS0067 // never raised by this double
        public event EventHandler? DevicesChanged;
#pragma warning restore CS0067

        public IReadOnlyList<MicrophoneInfo> GetAvailableMicrophones() => [];

        public MicrophoneInfo? GetDefaultMicrophone() => null;

        public void Dispose() { }
    }

    /// <summary>Reports all non-zero audio as one speech span.</summary>
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

    private sealed class FixedTextRecognizer(string text) : ISpeechRecognizer
    {
        public bool IsReady => true;

        public Task InitializeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<TranscriptionResult> TranscribeAsync(
            ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
            => Task.FromResult(new TranscriptionResult { Text = text });

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    /// <summary>Cleanup double: records what it was asked and answers with a scripted reply.</summary>
    private sealed class FakeCleanupService : ISpeechCleanupService
    {
        public Func<string, string> Reply { get; set; } = _ => CleanText;
        public List<(string Transcript, SpeechCleanupOptions Options)> Calls { get; } = [];
        public int PrepareCalls;

        public Task<SpeechCleanupReadiness> GetReadinessAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(SpeechCleanupReadiness.Ready);

        public Task PrepareAsync(CancellationToken cancellationToken = default)
        {
            Interlocked.Increment(ref PrepareCalls);
            return Task.CompletedTask;
        }

        public Task<string> CleanAsync(
            string transcript, SpeechCleanupOptions options, CancellationToken cancellationToken = default)
        {
            lock (Calls)
                Calls.Add((transcript, options));
            return Task.FromResult(Reply(transcript));
        }

        public Task UnloadAsync() => Task.CompletedTask;
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (Messages)
                Messages.Add(formatter(state, exception) + (exception is null ? "" : " " + exception));
        }
    }

    private static float[] Speech(int durationMs)
    {
        var samples = new float[16_000 * durationMs / 1000];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = MathF.Sin(2 * MathF.PI * 440 * i / 16_000f) * 0.5f;
        return samples;
    }

    /// <summary>
    /// Runs one utterance through a pipeline and returns the text it emitted,
    /// or null when nothing was emitted.
    /// </summary>
    private static async Task<(string? Text, CapturingLogger<AudioPipelineService> Log)> DictateAsync(
        string recognizedText,
        ISpeechCleanupService? cleanup,
        Action<FakeSettingsService>? configure = null)
    {
        var capture = new TestAudioCaptureService();
        await using var vad = new SpanVadService();
        await using var recognizer = new FixedTextRecognizer(recognizedText);
        var settings = new FakeSettingsService();
        configure?.Invoke(settings);
        var logger = new CapturingLogger<AudioPipelineService>();

        await using var pipeline = new AudioPipelineService(
            capture, vad, recognizer, settings,
            new FakeKeyboardLayoutService(), new FakeMicrophoneEnumerator(), logger, cleanup);

        var emitted = new TaskCompletionSource<string>();
        pipeline.TranscriptionAvailable += (_, e) => emitted.TrySetResult(e.Result.Text);

        await pipeline.StartAsync(PipelineMode.SingleUtterance);
        capture.SimulateAudioData(Speech(1000));
        await pipeline.StopAsync();

        var done = await Task.WhenAny(emitted.Task, Task.Delay(TimeSpan.FromSeconds(2)));
        return (done == emitted.Task ? await emitted.Task : null, logger);
    }

    private static void Enable(FakeSettingsService settings) =>
        settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, "True").GetAwaiter().GetResult();

    [Fact]
    public async Task Enabled_EmitsTheCleanedText()
    {
        var cleanup = new FakeCleanupService();

        var (text, _) = await DictateAsync(RawText, cleanup, Enable);

        Assert.Equal(CleanText, text);
        var call = Assert.Single(cleanup.Calls);
        Assert.Equal(RawText, call.Transcript);
        Assert.Equal(SpeechCleanupOptions.Default, call.Options);
    }

    [Fact]
    public async Task Enabled_PassesTheSavedStyleToTheModel()
    {
        var cleanup = new FakeCleanupService();

        await DictateAsync(RawText, cleanup, s =>
        {
            Enable(s);
            s.SetAsync(SettingsKeys.SpeechCleanupStyling, "Formal").GetAwaiter().GetResult();
            s.SetAsync(SettingsKeys.SpeechCleanupStructure, "Lists").GetAwaiter().GetResult();
            s.SetAsync(SettingsKeys.SpeechCleanupContext, "Email").GetAwaiter().GetResult();
        });

        Assert.Equal(
            new SpeechCleanupOptions(CleanupStyling.Formal, CleanupStructure.Lists, CleanupContext.Email),
            Assert.Single(cleanup.Calls).Options);
    }

    [Fact]
    public async Task Enabled_StartsLoadingTheModelWhenRecordingStarts()
    {
        var cleanup = new FakeCleanupService();

        await DictateAsync(RawText, cleanup, Enable);

        // Fire-and-forget on a pool thread; give it a moment to land.
        for (int i = 0; i < 100 && Volatile.Read(ref cleanup.PrepareCalls) == 0; i++)
            await Task.Delay(10);
        Assert.Equal(1, Volatile.Read(ref cleanup.PrepareCalls));
    }

    [Fact]
    public async Task Off_LeavesTheTextAlone_AndNeverTouchesTheModel()
    {
        var cleanup = new FakeCleanupService();

        var (text, _) = await DictateAsync(RawText, cleanup);

        Assert.Equal(RawText, text);
        Assert.Empty(cleanup.Calls);
        Assert.Equal(0, cleanup.PrepareCalls);
    }

    [Fact]
    public async Task Enabled_ButNoServiceRegistered_LeavesTheTextAlone()
    {
        var (text, _) = await DictateAsync(RawText, cleanup: null, Enable);

        Assert.Equal(RawText, text);
    }

    [Fact]
    public async Task ModelFailure_FallsBackToTheRawText()
    {
        var cleanup = new FakeCleanupService
        {
            Reply = _ => throw new InvalidOperationException("Speech cleanup request failed (HTTP 500)."),
        };

        var (text, _) = await DictateAsync(RawText, cleanup, Enable);

        Assert.Equal(RawText, text);
    }

    [Fact]
    public async Task Timeout_FallsBackToTheRawText()
    {
        var cleanup = new FakeCleanupService { Reply = _ => throw new TimeoutException() };

        var (text, _) = await DictateAsync(RawText, cleanup, Enable);

        Assert.Equal(RawText, text);
    }

    [Fact]
    public async Task ImplausibleResult_IsRejectedInFavourOfTheRawText()
    {
        var cleanup = new FakeCleanupService
        {
            Reply = raw => string.Concat(Enumerable.Repeat(raw + " ", 10)),
        };

        var (text, _) = await DictateAsync(RawText, cleanup, Enable);

        Assert.Equal(RawText, text);
    }

    [Fact]
    public async Task NonEnglishText_IsNotSentToTheModel()
    {
        const string russian = "мне нужно отправить отчёт до пятницы";
        var cleanup = new FakeCleanupService();

        var (text, _) = await DictateAsync(russian, cleanup, Enable);

        Assert.Equal(russian, text);
        Assert.Empty(cleanup.Calls);
    }

    [Fact]
    public async Task FillerOnly_CleansToNothing_AndNothingIsEmitted()
    {
        var cleanup = new FakeCleanupService { Reply = _ => string.Empty };

        var (text, _) = await DictateAsync("um uh hmm", cleanup, Enable);

        Assert.Null(text);
    }

    [Fact]
    public async Task PunctuationStripping_RunsAfterCleanup()
    {
        var cleanup = new FakeCleanupService();

        var (text, _) = await DictateAsync(RawText, cleanup, s =>
        {
            Enable(s);
            s.SetAsync(SettingsKeys.AutomaticPunctuation, "False").GetAwaiter().GetResult();
        });

        // The model added the full stop; the user's "no punctuation" has the last word.
        Assert.Equal("I need to send the report by Thursday", text);
    }

    /// <summary>
    /// Dictated text is user-private (ADR-046): neither the raw transcript nor
    /// the cleaned one may reach a log, on the success path or the failure paths.
    /// </summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Cleanup_NeverLogsEitherText(bool fail)
    {
        var cleanup = new FakeCleanupService();
        if (fail)
            cleanup.Reply = raw => string.Concat(Enumerable.Repeat(raw + " ", 10));

        var (_, logger) = await DictateAsync(RawText, cleanup, Enable);

        lock (logger.Messages)
        {
            Assert.DoesNotContain(logger.Messages, m => m.Contains("report"));
            Assert.Contains(logger.Messages, m => m.Contains("Speech cleanup"));
        }
    }
}
