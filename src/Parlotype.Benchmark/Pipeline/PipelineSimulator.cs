using Parlotype.Core.Audio;

namespace Parlotype.Benchmark.Pipeline;

/// <summary>
/// Simulates real-time AudioPipelineService behavior by feeding audio in 10ms callbacks,
/// running VAD in 500ms chunks, and flushing when silence exceeds the threshold.
/// No clamping is applied — the raw silenceThresholdMs value is used.
/// </summary>
public static class PipelineSimulator
{
    private const int SampleRate = 16_000;
    private const int VadMinChunkSamples = 8_000;  // 500ms
    private const int SegmentMergeTolerance = 1024;  // 64ms
    private const int DefaultMaxBufferSeconds = 30;
    private const int CallbackSamples = 160;  // 10ms at 16kHz

    /// <summary>
    /// Simulates real-time AudioPipelineService behavior by feeding audio in 10ms callbacks,
    /// running VAD in 500ms chunks, and flushing when silence exceeds the threshold.
    /// No clamping is applied — the raw silenceThresholdMs value is used.
    /// </summary>
    /// <param name="maxBufferSeconds">Force-flush ceiling, mirroring
    /// <c>AudioPipelineService.MaxBatchBufferSamples</c>. Swept rather than fixed so the
    /// hold-scoped push-to-talk work can measure what the ceiling costs per engine.</param>
    /// <returns>List of audio segments, one per flush event.</returns>
    public static List<float[]> Simulate(
        ReadOnlySpan<float> audioSamples,
        IVadService vadService,
        VadOptions vadOptions,
        int silenceThresholdMs,
        int maxBufferSeconds = DefaultMaxBufferSeconds)
    {
        int silenceThresholdSamples = silenceThresholdMs * SampleRate / 1000;
        int maxBatchBufferSamples = maxBufferSeconds * SampleRate;

        var results = new List<float[]>();
        var buffer = new List<float>();
        int vadProcessedUpTo = 0;
        var accumulatedSegments = new List<VadSpeechSegment>();

        // Feed audio in CallbackSamples (160 samples = 10ms) chunks
        int position = 0;
        while (position < audioSamples.Length)
        {
            int chunkSize = Math.Min(CallbackSamples, audioSamples.Length - position);
            var chunk = audioSamples.Slice(position, chunkSize);
            position += chunkSize;

            // Append samples to buffer
            for (int i = 0; i < chunk.Length; i++)
                buffer.Add(chunk[i]);

            // Run VAD when enough new samples have accumulated
            int newSamplesCount = buffer.Count - vadProcessedUpTo;
            if (newSamplesCount >= VadMinChunkSamples)
            {
                var bufferSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(buffer);
                var newSegments = vadService.DetectSpeech(bufferSpan[vadProcessedUpTo..], vadOptions);

                foreach (var seg in newSegments)
                {
                    var adjusted = new VadSpeechSegment(
                        seg.StartSample + vadProcessedUpTo,
                        seg.EndSample + vadProcessedUpTo);
                    MergeOrAddSegment(accumulatedSegments, adjusted);
                }

                vadProcessedUpTo = buffer.Count;
            }

            // No speech and buffer too large — discard
            if (accumulatedSegments.Count == 0 && buffer.Count > maxBatchBufferSamples)
            {
                buffer.Clear();
                vadProcessedUpTo = 0;
                accumulatedSegments.Clear();
                continue;
            }

            // Check silence after speech
            if (accumulatedSegments.Count > 0)
            {
                var lastSegment = accumulatedSegments[^1];
                int silenceAfterSpeech = buffer.Count - lastSegment.EndSample;

                if (silenceAfterSpeech >= silenceThresholdSamples)
                {
                    var bufferSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(buffer);
                    var speechSamples = SpeechSegmentExtractor.Extract(
                        bufferSpan, accumulatedSegments, vadOptions.InterSegmentSilenceMs);
                    results.Add(speechSamples);
                    buffer.Clear();
                    vadProcessedUpTo = 0;
                    accumulatedSegments.Clear();
                    continue;
                }
            }

            // Force-flush if buffer is too large with speech
            if (buffer.Count > maxBatchBufferSamples)
            {
                var speechSamples = buffer.ToArray();
                results.Add(speechSamples);
                buffer.Clear();
                vadProcessedUpTo = 0;
                accumulatedSegments.Clear();
            }
        }

        // Final flush: rerun VAD on the entire remaining buffer (mirrors AudioPipelineService.FlushBuffer)
        if (buffer.Count >= 1024)
        {
            var bufferSpan = System.Runtime.InteropServices.CollectionsMarshal.AsSpan(buffer);
            var finalSegments = vadService.DetectSpeech(bufferSpan, vadOptions);
            if (finalSegments.Count > 0)
            {
                var speechSamples = SpeechSegmentExtractor.Extract(
                    bufferSpan, finalSegments, vadOptions.InterSegmentSilenceMs);
                results.Add(speechSamples);
            }
        }

        return results;
    }

    private static void MergeOrAddSegment(List<VadSpeechSegment> segments, VadSpeechSegment segment)
    {
        if (segments.Count > 0)
        {
            var last = segments[^1];
            if (segment.StartSample <= last.EndSample + SegmentMergeTolerance)
            {
                segments[^1] = new VadSpeechSegment(
                    last.StartSample,
                    Math.Max(last.EndSample, segment.EndSample));
                return;
            }
        }

        segments.Add(segment);
    }
}
