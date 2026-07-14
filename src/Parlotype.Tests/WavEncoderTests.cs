using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class WavEncoderTests
{
    [Fact]
    public void Encode_ProducesValidWavHeader()
    {
        // 1 second of silence at 16kHz
        var samples = new float[16_000];
        var wav = WavEncoder.Encode(samples, sampleRate: 16_000);

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
    public void Encode_ClampsValues()
    {
        var samples = new float[] { 2.0f, -2.0f, 0.5f, -0.5f };
        var wav = WavEncoder.Encode(samples, sampleRate: 16_000);

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
    public void Encode_EmptySamples_ProducesValidHeader()
    {
        var wav = WavEncoder.Encode(ReadOnlySpan<float>.Empty, sampleRate: 16_000);

        Assert.Equal(44, wav.Length); // Header only, no data
        var dataSize = BitConverter.ToInt32(wav, 40);
        Assert.Equal(0, dataSize);
    }

    public static TheoryData<string, float[], int> EquivalenceCases => new()
    {
        { "empty", [], 16_000 },
        { "single", [0.25f], 16_000 },
        { "clipping", [2.0f, -2.0f, 1.0f, -1.0f, 1.5f, -1.5f], 16_000 },
        { "sine-1s", SineWave(16_000), 16_000 },
        { "sine-10s-24kHz", SineWave(240_000), 24_000 },
    };

    /// <summary>
    /// The 2026-07 rewrite (exact-size array + BinaryPrimitives, plan
    /// 2026-07-11-audio-pipeline-perf-security P5) must be byte-identical to the
    /// original MemoryStream + BinaryWriter encoder, verified against a frozen
    /// copy of that implementation.
    /// </summary>
    [Theory]
    [MemberData(nameof(EquivalenceCases))]
    public void Encode_MatchesLegacyImplementation_ByteForByte(string label, float[] samples, int sampleRate)
    {
        var expected = LegacyEncode(samples, sampleRate);
        var actual = WavEncoder.Encode(samples, sampleRate);

        Assert.True(expected.SequenceEqual(actual), $"case '{label}' diverged from legacy encoder output");
    }

    private static float[] SineWave(int count)
    {
        var samples = new float[count];
        for (int i = 0; i < count; i++)
            samples[i] = MathF.Sin(2 * MathF.PI * 440 * i / 16_000f) * 0.8f;
        return samples;
    }

    /// <summary>Frozen copy of the pre-rewrite encoder. Do not modify.</summary>
    private static byte[] LegacyEncode(ReadOnlySpan<float> samples, int sampleRate)
    {
        const int bitsPerSample = 16;
        const int numChannels = 1;
        const int bytesPerSample = bitsPerSample / 8;
        var dataSize = samples.Length * bytesPerSample;

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);

        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);

        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)numChannels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * numChannels * bytesPerSample);
        bw.Write((short)(numChannels * bytesPerSample));
        bw.Write((short)bitsPerSample);

        bw.Write("data"u8);
        bw.Write(dataSize);

        for (int i = 0; i < samples.Length; i++)
        {
            var clamped = Math.Clamp(samples[i], -1.0f, 1.0f);
            bw.Write((short)(clamped * 32767));
        }

        return ms.ToArray();
    }
}
