using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Parlotype.Tests;

/// <summary>Helpers for loading test audio resources.</summary>
internal static class TestAudioHelper
{
    private static readonly string ResourcesDir = Path.Combine(
        AppContext.BaseDirectory, "resources");

    public static string GetWavPath(string fileName)
    {
        var path = Path.Combine(ResourcesDir, fileName);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Test resource not found: {path}");
        return path;
    }

    /// <summary>Loads a WAV file as 16-bit mono PCM bytes, converting from stereo if needed.</summary>
    public static byte[] LoadWavAsPcmBytes(string fileName)
    {
        using var reader = new WaveFileReader(GetWavPath(fileName));
        var sampleProvider = reader.ToSampleProvider();

        // Convert to mono if stereo
        if (sampleProvider.WaveFormat.Channels > 1)
            sampleProvider = sampleProvider.ToMono();

        // Resample to 16kHz if needed
        ISampleProvider finalProvider = sampleProvider.WaveFormat.SampleRate != 16_000
            ? new WdlResamplingSampleProvider(sampleProvider, 16_000)
            : sampleProvider;

        // Read all float samples
        var floatSamples = ReadAllSamples(finalProvider);

        // Convert to 16-bit PCM bytes
        var pcmBytes = new byte[floatSamples.Length * 2];
        for (int i = 0; i < floatSamples.Length; i++)
        {
            var clamped = Math.Clamp(floatSamples[i], -1.0f, 1.0f);
            short shortSample = (short)(clamped * short.MaxValue);
            pcmBytes[i * 2] = (byte)(shortSample & 0xFF);
            pcmBytes[i * 2 + 1] = (byte)((shortSample >> 8) & 0xFF);
        }

        return pcmBytes;
    }

    /// <summary>Loads a WAV file as mono float samples normalized to [-1, 1].</summary>
    public static float[] LoadWavAsFloatSamples(string fileName)
    {
        using var reader = new WaveFileReader(GetWavPath(fileName));
        var sampleProvider = reader.ToSampleProvider();

        // Convert to mono if stereo
        if (sampleProvider.WaveFormat.Channels > 1)
            sampleProvider = sampleProvider.ToMono();

        // Resample to 16kHz if needed
        ISampleProvider finalProvider = sampleProvider.WaveFormat.SampleRate != 16_000
            ? new WdlResamplingSampleProvider(sampleProvider, 16_000)
            : sampleProvider;

        return ReadAllSamples(finalProvider);
    }

    private static float[] ReadAllSamples(ISampleProvider provider)
    {
        var samples = new List<float>();
        var buffer = new float[4096];
        int read;
        while ((read = provider.Read(buffer, 0, buffer.Length)) > 0)
        {
            samples.AddRange(buffer.AsSpan(0, read).ToArray());
        }
        return samples.ToArray();
    }
}
