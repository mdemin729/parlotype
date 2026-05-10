using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class LlamaCppSpeechRecognizerTests
{
    [Fact]
    public void EncodeWav_ProduceValidWavHeader()
    {
        // 1 second of silence at 16kHz
        var samples = new float[16_000];
        var wav = LlamaCppSpeechRecognizer.EncodeWav(samples, sampleRate: 16_000);

        // RIFF header: 44 bytes + data
        Assert.True(wav.Length >= 44);

        // "RIFF" magic
        Assert.Equal((byte)'R', wav[0]);
        Assert.Equal((byte)'I', wav[1]);
        Assert.Equal((byte)'F', wav[2]);
        Assert.Equal((byte)'F', wav[3]);

        // "WAVE" format
        Assert.Equal((byte)'W', wav[8]);
        Assert.Equal((byte)'A', wav[9]);
        Assert.Equal((byte)'V', wav[10]);
        Assert.Equal((byte)'E', wav[11]);

        // PCM format (1)
        var format = BitConverter.ToInt16(wav, 20);
        Assert.Equal(1, format);

        // Mono channel
        var channels = BitConverter.ToInt16(wav, 22);
        Assert.Equal(1, channels);

        // Sample rate
        var sampleRate = BitConverter.ToInt32(wav, 24);
        Assert.Equal(16_000, sampleRate);

        // Bits per sample
        var bitsPerSample = BitConverter.ToInt16(wav, 34);
        Assert.Equal(16, bitsPerSample);

        // Data size = 16000 samples * 2 bytes
        var dataSize = BitConverter.ToInt32(wav, 40);
        Assert.Equal(32_000, dataSize);

        // Total file = 44 header + 32000 data
        Assert.Equal(44 + 32_000, wav.Length);
    }

    [Fact]
    public void EncodeWav_ClampsValues()
    {
        var samples = new float[] { 2.0f, -2.0f, 0.5f, -0.5f };
        var wav = LlamaCppSpeechRecognizer.EncodeWav(samples, sampleRate: 16_000);

        // Read the PCM16 values from data section (offset 44)
        var s0 = BitConverter.ToInt16(wav, 44);
        var s1 = BitConverter.ToInt16(wav, 46);
        var s2 = BitConverter.ToInt16(wav, 48);
        var s3 = BitConverter.ToInt16(wav, 50);

        // Clamped to max/min
        Assert.Equal(32767, s0);
        Assert.Equal(-32767, s1);

        // 0.5 * 32767 ≈ 16383
        Assert.Equal(16383, s2);
        Assert.Equal(-16383, s3);
    }

    [Fact]
    public void EncodeWav_EmptySamples_ProducesValidHeader()
    {
        var wav = LlamaCppSpeechRecognizer.EncodeWav(ReadOnlySpan<float>.Empty, sampleRate: 16_000);

        Assert.Equal(44, wav.Length); // Header only, no data
        var dataSize = BitConverter.ToInt32(wav, 40);
        Assert.Equal(0, dataSize);
    }
}
