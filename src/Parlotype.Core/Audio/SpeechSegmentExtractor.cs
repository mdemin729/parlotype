namespace Parlotype.Core.Audio;

/// <summary>
/// Extracts speech samples from audio buffer using VAD segment boundaries,
/// optionally inserting silence between segments to preserve prosodic context.
/// </summary>
public static class SpeechSegmentExtractor
{
    private const int SampleRate = 16_000;

    /// <summary>
    /// Extracts speech segments from audio and concatenates them with inter-segment silence.
    /// </summary>
    /// <param name="buffer">Source audio samples.</param>
    /// <param name="segments">VAD-detected speech segments.</param>
    /// <param name="interSegmentSilenceMs">Milliseconds of silence to insert between segments. 0 = no gap (original behavior).</param>
    /// <returns>Concatenated speech samples with silence gaps between segments.</returns>
    public static float[] Extract(
        ReadOnlySpan<float> buffer,
        IReadOnlyList<VadSpeechSegment> segments,
        int interSegmentSilenceMs = 160)
    {
        if (segments.Count == 0)
            return [];

        int silenceSamples = interSegmentSilenceMs * SampleRate / 1000;

        // Calculate total output size
        long totalSpeech = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            int start = Math.Max(0, segments[i].StartSample);
            int end = Math.Min(buffer.Length, segments[i].EndSample);
            if (end > start)
                totalSpeech += end - start;
        }

        int gapCount = Math.Max(0, segments.Count - 1);
        long totalLength = totalSpeech + (long)gapCount * silenceSamples;
        var result = new float[totalLength];

        int offset = 0;
        for (int i = 0; i < segments.Count; i++)
        {
            int start = Math.Max(0, segments[i].StartSample);
            int end = Math.Min(buffer.Length, segments[i].EndSample);
            int length = end - start;

            if (length > 0)
            {
                buffer.Slice(start, length).CopyTo(result.AsSpan(offset));
                offset += length;
            }

            // Insert silence gap between segments (not after the last one)
            if (i < segments.Count - 1 && silenceSamples > 0)
            {
                // result is zero-initialized by the CLR, so silence is automatic
                offset += silenceSamples;
            }
        }

        return result;
    }
}
