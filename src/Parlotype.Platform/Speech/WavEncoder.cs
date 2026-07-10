namespace Parlotype.Platform.Speech;

/// <summary>
/// Encodes float PCM samples into 16-bit mono WAV byte arrays. Shared by every
/// recognizer that hands audio to an out-of-process or remote service as a
/// file rather than raw samples (llama-server, the OpenAI-compatible cloud
/// provider, xAI Grok).
/// </summary>
internal static class WavEncoder
{
    /// <summary>Encodes float PCM samples into a 16-bit mono WAV byte array.</summary>
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
