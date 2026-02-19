using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Platform.Speech;
using Whisper.net.Ggml;
using Xunit;

namespace Parlotype.Tests;

public class WhisperSpeechRecognizerTests
{
    [Fact]
    public async Task TranscribeAsync_WithSpeechAudio_ReturnsTranscription()
    {
        await using var recognizer = new WhisperSpeechRecognizer(NullLogger<WhisperSpeechRecognizer>.Instance, GgmlType.BaseEn);
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
        await using var recognizer = new WhisperSpeechRecognizer(NullLogger<WhisperSpeechRecognizer>.Instance, GgmlType.BaseEn);
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
