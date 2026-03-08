namespace Parlotype.Core.Audio;

/// <summary>
/// Detects voice activity in audio samples to filter silence before speech recognition.
/// </summary>
public interface IVadService : IAsyncDisposable
{
    /// <summary>Detects speech segments using default VAD options.</summary>
    /// <param name="samples">Audio samples as 16 kHz mono float32.</param>
    /// <returns>List of speech segments identified by sample indices.</returns>
    List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples);

    /// <summary>Detects speech segments using the specified VAD options.</summary>
    /// <param name="samples">Audio samples as 16 kHz mono float32.</param>
    /// <param name="options">VAD configuration parameters.</param>
    /// <returns>List of speech segments identified by sample indices.</returns>
    List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples, VadOptions options);
}
