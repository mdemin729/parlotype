namespace Parlotype.MicroBenchmarks;

/// <summary>Deterministic synthetic audio so runs are comparable across machines and sessions.</summary>
internal static class SyntheticAudio
{
    public const int SampleRate = 16_000;

    /// <summary>Seeded sine + noise in [-1, 1], resembling speech-band energy.</summary>
    public static float[] Generate(int sampleCount, int seed = 42)
    {
        var rng = new Random(seed);
        var samples = new float[sampleCount];
        for (int i = 0; i < sampleCount; i++)
        {
            var t = i / (double)SampleRate;
            var tone = 0.4 * Math.Sin(2 * Math.PI * 220 * t) + 0.2 * Math.Sin(2 * Math.PI * 730 * t);
            var noise = (rng.NextDouble() - 0.5) * 0.1;
            samples[i] = (float)(tone + noise);
        }

        return samples;
    }
}
