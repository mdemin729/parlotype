using System.Buffers.Binary;
using System.Runtime.InteropServices;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Encodes float PCM samples into 16-bit mono WAV byte arrays. Shared by every
/// recognizer that hands audio to an out-of-process or remote service as a
/// file rather than raw samples (llama-server, the OpenAI-compatible cloud
/// provider, xAI Grok).
/// </summary>
/// <remarks>
/// Writes into a single exact-size array — the previous MemoryStream +
/// per-sample BinaryWriter implementation allocated 2× the WAV size and made a
/// virtual stream write per sample (finding P5,
/// plans/2026-07-11-audio-pipeline-perf-security). Output is byte-identical.
/// </remarks>
internal static class WavEncoder
{
    private const int HeaderSize = 44;

    /// <summary>Encodes float PCM samples into a 16-bit mono WAV byte array.</summary>
    internal static byte[] Encode(ReadOnlySpan<float> samples, int sampleRate)
    {
        const int bitsPerSample = 16;
        const int numChannels = 1;
        const int bytesPerSample = bitsPerSample / 8;
        var dataSize = samples.Length * bytesPerSample;

        var wav = new byte[HeaderSize + dataSize];
        var span = wav.AsSpan();

        // RIFF header
        "RIFF"u8.CopyTo(span);
        BinaryPrimitives.WriteInt32LittleEndian(span[4..], 36 + dataSize);
        "WAVE"u8.CopyTo(span[8..]);

        // fmt chunk
        "fmt "u8.CopyTo(span[12..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[16..], 16);
        BinaryPrimitives.WriteInt16LittleEndian(span[20..], 1); // PCM
        BinaryPrimitives.WriteInt16LittleEndian(span[22..], numChannels);
        BinaryPrimitives.WriteInt32LittleEndian(span[24..], sampleRate);
        BinaryPrimitives.WriteInt32LittleEndian(span[28..], sampleRate * numChannels * bytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(span[32..], numChannels * bytesPerSample);
        BinaryPrimitives.WriteInt16LittleEndian(span[34..], bitsPerSample);

        // data chunk
        "data"u8.CopyTo(span[36..]);
        BinaryPrimitives.WriteInt32LittleEndian(span[40..], dataSize);

        var data = span[HeaderSize..];
        if (BitConverter.IsLittleEndian)
        {
            var pcm = MemoryMarshal.Cast<byte, short>(data);
            for (int i = 0; i < samples.Length; i++)
                pcm[i] = (short)(Math.Clamp(samples[i], -1.0f, 1.0f) * 32767);
        }
        else
        {
            for (int i = 0; i < samples.Length; i++)
                BinaryPrimitives.WriteInt16LittleEndian(
                    data[(i * bytesPerSample)..],
                    (short)(Math.Clamp(samples[i], -1.0f, 1.0f) * 32767));
        }

        return wav;
    }
}
