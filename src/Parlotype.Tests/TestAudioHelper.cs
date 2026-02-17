using NAudio.Wave;

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

    /// <summary>Loads a WAV file as 16-bit PCM bytes (skipping the WAV header).</summary>
    public static byte[] LoadWavAsPcmBytes(string fileName)
    {
        using var reader = new WaveFileReader(GetWavPath(fileName));
        using var ms = new MemoryStream();
        reader.CopyTo(ms);
        return ms.ToArray();
    }

    /// <summary>Loads a WAV file as float samples normalized to [-1, 1].</summary>
    public static float[] LoadWavAsFloatSamples(string fileName)
    {
        var pcmBytes = LoadWavAsPcmBytes(fileName);
        int sampleCount = pcmBytes.Length / 2;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
            samples[i] = sample / (float)short.MaxValue;
        }

        return samples;
    }
}
