namespace Parlotype.MicroBenchmarks;

/// <summary>
/// Frozen copy of <c>Parlotype.Platform.Speech.WavEncoder</c> as it stood
/// before the 2026-07 allocation work (plan 2026-07-11-audio-pipeline-perf-security,
/// finding P5): MemoryStream + per-sample BinaryWriter writes + a final
/// ToArray copy. Kept verbatim so before/after appear in one benchmark run.
/// Do not "fix" this class.
/// </summary>
internal static class LegacyWavEncoder
{
    internal static byte[] Encode(ReadOnlySpan<float> samples, int sampleRate)
    {
        const int bitsPerSample = 16;
        const int numChannels = 1;
        const int bytesPerSample = bitsPerSample / 8;
        var dataSize = samples.Length * bytesPerSample;

        using var ms = new MemoryStream(44 + dataSize);
        using var bw = new BinaryWriter(ms);

        // RIFF header
        bw.Write("RIFF"u8);
        bw.Write(36 + dataSize);
        bw.Write("WAVE"u8);

        // fmt chunk
        bw.Write("fmt "u8);
        bw.Write(16);
        bw.Write((short)1); // PCM
        bw.Write((short)numChannels);
        bw.Write(sampleRate);
        bw.Write(sampleRate * numChannels * bytesPerSample);
        bw.Write((short)(numChannels * bytesPerSample));
        bw.Write((short)bitsPerSample);

        // data chunk
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
