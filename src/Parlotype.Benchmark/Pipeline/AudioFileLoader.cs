using NAudio.Wave;
using NAudio.Wave.SampleProviders;

namespace Parlotype.Benchmark.Pipeline;

/// <summary>Loads WAV audio files and resamples to 16 kHz mono float samples.</summary>
public static class AudioFileLoader
{
    private const int TargetSampleRate = 16_000;

    /// <summary>
    /// Loads a WAV file and returns 16 kHz mono float samples with the audio duration.
    /// </summary>
    /// <param name="filePath">Path to the WAV file.</param>
    /// <returns>Tuple of float samples ([-1, 1] normalized) and duration in seconds.</returns>
    public static (float[] Samples, double DurationSeconds) LoadWav(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException($"Audio file not found: {filePath}", filePath);

        using var reader = new WaveFileReader(filePath);
        var sampleProvider = reader.ToSampleProvider();

        // Convert to mono if multichannel
        if (sampleProvider.WaveFormat.Channels > 1)
            sampleProvider = sampleProvider.ToMono();

        // Resample to 16 kHz if needed
        ISampleProvider finalProvider = sampleProvider.WaveFormat.SampleRate != TargetSampleRate
            ? new WdlResamplingSampleProvider(sampleProvider, TargetSampleRate)
            : sampleProvider;

        var samples = ReadAllSamples(finalProvider);
        var durationSeconds = (double)samples.Length / TargetSampleRate;

        return (samples, durationSeconds);
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
