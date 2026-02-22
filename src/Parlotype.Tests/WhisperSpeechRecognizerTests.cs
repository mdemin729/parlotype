using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class WhisperSpeechRecognizerTests
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

    /// <summary>Headless download service that downloads without showing UI.</summary>
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

    [Fact]
    public async Task TranscribeAsync_WithSpeechAudio_ReturnsTranscription()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.BaseEn.ToString());

        await using var recognizer = new WhisperSpeechRecognizer(new HeadlessModelDownloadService(), settings, NullLogger<WhisperSpeechRecognizer>.Instance);
        await recognizer.InitializeAsync();

        Assert.True(recognizer.IsReady);

        var pcmBytes = TestAudioHelper.LoadWavAsPcmBytes("kennedy.wav");
        var result = await recognizer.TranscribeAsync(pcmBytes);

        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Text));

        // Whisper tiny model produces rough transcriptions;
        // verify we get meaningful output (not blank/empty)
        Assert.True(result.Text.Trim().Length > 5,
            $"Expected meaningful transcription but got: '{result.Text}'");
        Assert.True(result.Text.Contains("I believe"),
            $"Expected transcription to start with but \"I believe\" got: '{result.Text}'");
    }

    [Fact]
    public async Task TranscribeAsync_WithSilence_ReturnsEmptyOrMinimalText()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.BaseEn.ToString());

        await using var recognizer = new WhisperSpeechRecognizer(new HeadlessModelDownloadService(), settings, NullLogger<WhisperSpeechRecognizer>.Instance);
        await recognizer.InitializeAsync();

        // 1 second of silence as 16-bit PCM bytes
        var silenceBytes = new byte[16_000 * 2];
        var result = await recognizer.TranscribeAsync(silenceBytes);

        Assert.NotNull(result);
        // Silence may produce empty text or Whisper special tokens like [BLANK_AUDIO]
        var cleanText = result.Text.Trim().Replace("[BLANK_AUDIO]", "").Trim();
        Assert.True(cleanText.Length < 10,
            $"Expected near-empty text for silence but got: '{result.Text}'");
    }
}
