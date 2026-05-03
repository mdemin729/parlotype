using Parlotype.Benchmark.Pipeline;
using Parlotype.Core.Audio;

namespace Parlotype.Benchmark.Tests;

public class PipelineSimulatorTests
{
    private static readonly VadOptions DefaultVadOptions = new()
    {
        Threshold = 0.5f,
        SpeechPadMs = 0,       // No padding for deterministic tests
        MinSilenceDurationMs = 0,
        MinSpeechDurationMs = 0,
        InterSegmentSilenceMs = 0,
    };

    [Fact]
    public void Simulate_NoSpeech_ReturnsEmpty()
    {
        var silence = CreateSilence(2000); // 2 seconds of silence
        var vad = new FakeVadService();

        var result = PipelineSimulator.Simulate(silence.AsSpan(), vad, DefaultVadOptions, 500);

        Assert.Empty(result);
    }

    [Fact]
    public void Simulate_SpeechThenSilence_FlushesOnce()
    {
        // 1s speech + 1s silence (threshold = 500ms → should flush)
        var audio = Concat(CreateSpeech(1000), CreateSilence(1000));
        var vad = new FakeVadService();

        var result = PipelineSimulator.Simulate(audio.AsSpan(), vad, DefaultVadOptions, 500);

        Assert.Single(result);
        Assert.True(result[0].Length > 0);
    }

    [Fact]
    public void Simulate_SpeechSilenceSpeechSilence_FlushesTwice()
    {
        // 1s speech + 1s silence + 1s speech + 1s silence (threshold = 500ms)
        var audio = Concat(
            CreateSpeech(1000),
            CreateSilence(1000),
            CreateSpeech(1000),
            CreateSilence(1000));
        var vad = new FakeVadService();

        var result = PipelineSimulator.Simulate(audio.AsSpan(), vad, DefaultVadOptions, 500);

        Assert.Equal(2, result.Count);
        Assert.All(result, segment => Assert.True(segment.Length > 0));
    }

    [Fact]
    public void Simulate_LowThreshold_NoClamping_FlushesEarly()
    {
        // 1s speech + 200ms silence → with 100ms threshold (no clamping), should flush
        // If clamping were applied (min 500ms), this silence wouldn't trigger a flush
        var audio = Concat(CreateSpeech(1000), CreateSilence(200));
        var vad = new FakeVadService();

        var result = PipelineSimulator.Simulate(audio.AsSpan(), vad, DefaultVadOptions, 100);

        // Should flush at least once because the low threshold (100ms) is NOT clamped
        // to VadMinChunkSamples (500ms). With clamping, 200ms silence would never trigger
        // a flush — proving no clamping is applied.
        Assert.True(result.Count > 0,
            "Expected at least one flush with 100ms threshold (no clamping), but got none");
    }

    [Fact]
    public void Simulate_ShortSilence_NoFlush_UntilEnd()
    {
        // 1s speech + 200ms silence + 1s speech (threshold = 500ms)
        // The 200ms silence shouldn't trigger a mid-audio flush
        // Final flush should capture all speech
        var audio = Concat(
            CreateSpeech(1000),
            CreateSilence(200),
            CreateSpeech(1000));
        var vad = new FakeVadService();

        var result = PipelineSimulator.Simulate(audio.AsSpan(), vad, DefaultVadOptions, 500);

        // Only one segment from final flush (no mid-audio flush due to short silence)
        Assert.Single(result);
        Assert.True(result[0].Length > 0);
    }

    [Fact]
    public void Simulate_LongAudio_ForceFlushAt30s()
    {
        // 35 seconds of continuous speech → should force-flush at 30s boundary
        var audio = CreateSpeech(35_000);
        var vad = new FakeVadService();

        // Use a very large silence threshold so normal flush won't trigger
        var result = PipelineSimulator.Simulate(audio.AsSpan(), vad, DefaultVadOptions, 60_000);

        // At least one force-flush at 30s + final flush for remaining
        Assert.True(result.Count >= 2, $"Expected at least 2 flushes but got {result.Count}");
    }

    private static float[] CreateSpeech(int durationMs)
    {
        var samples = new float[16_000 * durationMs / 1000];
        for (int i = 0; i < samples.Length; i++)
            samples[i] = MathF.Sin(2 * MathF.PI * 440 * i / 16_000f) * 0.5f;
        return samples;
    }

    private static float[] CreateSilence(int durationMs) => new float[16_000 * durationMs / 1000];

    private static float[] Concat(params float[][] arrays)
    {
        var result = new float[arrays.Sum(a => a.Length)];
        int offset = 0;
        foreach (var arr in arrays)
        {
            arr.CopyTo(result, offset);
            offset += arr.Length;
        }
        return result;
    }

    /// <summary>
    /// FakeVadService for testing — detects speech in any region with non-zero samples.
    /// </summary>
    private sealed class FakeVadService : IVadService
    {
        public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples)
            => DetectSpeech(samples, new VadOptions());

        public List<VadSpeechSegment> DetectSpeech(ReadOnlySpan<float> samples, VadOptions options)
        {
            // Find contiguous non-zero regions
            var segments = new List<VadSpeechSegment>();
            int? start = null;
            for (int i = 0; i < samples.Length; i++)
            {
                if (Math.Abs(samples[i]) > 0.001f)
                {
                    start ??= i;
                }
                else if (start is not null)
                {
                    segments.Add(new VadSpeechSegment(start.Value, i));
                    start = null;
                }
            }
            if (start is not null)
                segments.Add(new VadSpeechSegment(start.Value, samples.Length));
            return segments;
        }

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
