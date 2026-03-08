using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Platform.Audio;
using Xunit;

namespace Parlotype.Tests;

public class SileroVadServiceTests
{
    [Fact]
    public async Task DetectSpeech_WithSpeechAudio_ReturnsSpeechSegments()
    {
        await using var vad = new SileroVadService(NullLogger<SileroVadService>.Instance);
        var samples = TestAudioHelper.LoadWavAsFloatSamples("kennedy.wav");

        var segments = vad.DetectSpeech(samples);

        Assert.NotEmpty(segments);
        Assert.All(segments, s =>
        {
            Assert.True(s.StartSample >= 0);
            Assert.True(s.EndSample > s.StartSample);
            Assert.True(s.EndSample <= samples.Length);
        });
    }

    [Fact]
    public async Task DetectSpeech_WithSilence_ReturnsNoSegments()
    {
        await using var vad = new SileroVadService(NullLogger<SileroVadService>.Instance);
        var silence = new float[16_000]; // 1 second of silence

        var segments = vad.DetectSpeech(silence);

        Assert.Empty(segments);
    }

    [Fact]
    public async Task DetectSpeech_Chunked_CoversSameRegionsAsBatch()
    {
        await using var vad = new SileroVadService(NullLogger<SileroVadService>.Instance);
        var samples = TestAudioHelper.LoadWavAsFloatSamples("kennedy.wav");

        // Reference: whole-buffer VAD
        var batchSegments = vad.DetectSpeech(samples);
        Assert.NotEmpty(batchSegments);

        int batchSpeechSamples = batchSegments.Sum(s => s.EndSample - s.StartSample);

        // Incremental: process in 8000-sample chunks (~500ms), accumulate with merge
        const int chunkSize = 8_000;
        const int mergeTolerance = 1024;
        var accumulated = new List<VadSpeechSegment>();
        int processed = 0;

        while (processed < samples.Length)
        {
            int remaining = Math.Min(chunkSize, samples.Length - processed);
            var chunk = samples.AsSpan(processed, remaining);

            var chunkSegments = vad.DetectSpeech(chunk);
            foreach (var seg in chunkSegments)
            {
                var adjusted = new VadSpeechSegment(
                    seg.StartSample + processed,
                    seg.EndSample + processed);

                if (accumulated.Count > 0)
                {
                    var last = accumulated[^1];
                    if (adjusted.StartSample <= last.EndSample + mergeTolerance)
                    {
                        accumulated[^1] = new VadSpeechSegment(
                            last.StartSample,
                            Math.Max(last.EndSample, adjusted.EndSample));
                        continue;
                    }
                }

                accumulated.Add(adjusted);
            }

            processed += remaining;
        }

        Assert.NotEmpty(accumulated);

        // Incremental detection should find at least 50% of the speech that batch finds.
        // Exact match is not expected because chunking loses cross-boundary context.
        int incrementalSpeechSamples = accumulated.Sum(s => s.EndSample - s.StartSample);
        double coverageRatio = (double)incrementalSpeechSamples / batchSpeechSamples;

        Assert.True(coverageRatio >= 0.5,
            $"Incremental VAD covered only {coverageRatio:P0} of batch speech ({incrementalSpeechSamples} vs {batchSpeechSamples} samples)");
    }

    [Fact]
    public async Task DetectSpeech_WithOptions_RespectsParameters()
    {
        await using var vad = new SileroVadService(NullLogger<SileroVadService>.Instance);
        var samples = TestAudioHelper.LoadWavAsFloatSamples("kennedy.wav");

        var defaultSegments = vad.DetectSpeech(samples);
        var lenientSegments = vad.DetectSpeech(samples, new VadOptions
        {
            MinSilenceDurationMs = 1000, // very lenient — fewer splits
            SpeechPadMs = 300,
        });

        Assert.NotEmpty(defaultSegments);
        Assert.NotEmpty(lenientSegments);
        // Lenient settings should produce fewer or equal segments
        Assert.True(lenientSegments.Count <= defaultSegments.Count,
            $"Expected lenient ({lenientSegments.Count}) <= default ({defaultSegments.Count})");
    }
}
