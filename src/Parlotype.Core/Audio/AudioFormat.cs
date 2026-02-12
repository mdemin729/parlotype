namespace Parlotype.Core.Audio;

/// <summary>Describes the format of a PCM audio stream.</summary>
public sealed record AudioFormat(int SampleRate, int Channels, int BitsPerSample)
{
    /// <summary>Whisper expects 16 kHz mono 16-bit PCM.</summary>
    public static AudioFormat Whisper => new(16_000, 1, 16);
}
