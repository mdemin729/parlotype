using Parlotype.Benchmark.Reporting;
using Parlotype.Benchmark.Results;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public class ResultComparerTests
{
    private static BenchmarkResult CreateResult(string runId, double avgWer, double avgCer, double avgRtf,
        double modelLoad = 500, double totalTime = 2000, double peakRam = 512,
        List<SampleResult>? samples = null, double? warmupTimeMs = null)
    {
        return new BenchmarkResult
        {
            RunId = runId,
            Timestamp = DateTimeOffset.UtcNow,
            Configuration = new BenchmarkConfig
            {
                Name = $"config-{runId}",
                Datasets = ["test"],
                Whisper = new WhisperConfig { Model = WhisperModelType.Base },
                Vad = new VadConfig { Enabled = true },
            },
            Environment = new EnvironmentInfo(),
            Summary = new BenchmarkSummary
            {
                TotalSamples = samples?.Count ?? 2,
                AverageWer = avgWer,
                AverageCer = avgCer,
                AverageRtf = avgRtf,
                ModelLoadTimeMs = modelLoad,
                WarmupTimeMs = warmupTimeMs,
                TotalProcessingTimeMs = totalTime,
                PeakRamMb = peakRam,
            },
            Samples = samples ?? [
                new SampleResult { Id = "s1", ReferenceText = "ref", HypothesisText = "hyp", Wer = avgWer, Cer = avgCer, ProcessingTimeMs = 1000, Rtf = avgRtf },
                new SampleResult { Id = "s2", ReferenceText = "ref2", HypothesisText = "hyp2", Wer = avgWer, Cer = avgCer, ProcessingTimeMs = 1000, Rtf = avgRtf },
            ],
        };
    }

    [Fact]
    public void Compare_ReturnsCorrectRunIds()
    {
        var a = CreateResult("run-a", 10, 5, 0.5);
        var b = CreateResult("run-b", 8, 4, 0.4);

        var comparison = ResultComparer.Compare(a, b);

        Assert.Equal("run-a", comparison.RunIdA);
        Assert.Equal("run-b", comparison.RunIdB);
    }

    [Fact]
    public void Compare_WerImproved_DeltaIsNegative()
    {
        var a = CreateResult("run-a", 10, 5, 0.5);
        var b = CreateResult("run-b", 8, 4, 0.4);

        var comparison = ResultComparer.Compare(a, b);

        Assert.Equal(-2, comparison.WerDelta.Absolute, 0.001);
        Assert.True(comparison.WerDelta.IsImproved);
    }

    [Fact]
    public void Compare_WerRegressed_DeltaIsPositive()
    {
        var a = CreateResult("run-a", 8, 4, 0.4);
        var b = CreateResult("run-b", 12, 6, 0.6);

        var comparison = ResultComparer.Compare(a, b);

        Assert.Equal(4, comparison.WerDelta.Absolute, 0.001);
        Assert.False(comparison.WerDelta.IsImproved);
    }

    [Fact]
    public void Compare_MatchesSamplesByIdCrossRuns()
    {
        var samplesA = new List<SampleResult>
        {
            new() { Id = "shared", ReferenceText = "r", HypothesisText = "h", Wer = 10, Cer = 5, ProcessingTimeMs = 100, Rtf = 0.5 },
            new() { Id = "only-a", ReferenceText = "r", HypothesisText = "h", Wer = 20, Cer = 10, ProcessingTimeMs = 100, Rtf = 0.5 },
        };
        var samplesB = new List<SampleResult>
        {
            new() { Id = "shared", ReferenceText = "r", HypothesisText = "h", Wer = 8, Cer = 4, ProcessingTimeMs = 100, Rtf = 0.4 },
            new() { Id = "only-b", ReferenceText = "r", HypothesisText = "h", Wer = 15, Cer = 7, ProcessingTimeMs = 100, Rtf = 0.3 },
        };

        var a = CreateResult("run-a", 15, 7.5, 0.5, samples: samplesA);
        var b = CreateResult("run-b", 11.5, 5.5, 0.35, samples: samplesB);

        var comparison = ResultComparer.Compare(a, b);

        Assert.Single(comparison.SampleDeltas);
        Assert.Equal("shared", comparison.SampleDeltas[0].SampleId);
        Assert.Equal(10, comparison.SampleDeltas[0].WerA);
        Assert.Equal(8, comparison.SampleDeltas[0].WerB);
    }

    [Fact]
    public void MetricDelta_RelativePercentage_ComputedCorrectly()
    {
        var delta = new MetricDelta(10, 8);

        Assert.Equal(-2, delta.Absolute, 0.001);
        Assert.NotNull(delta.Relative);
        Assert.Equal(-20, delta.Relative!.Value, 0.001);
        Assert.True(delta.IsImproved);
    }

    [Fact]
    public void MetricDelta_ZeroBaseline_RelativeIsNull()
    {
        var delta = new MetricDelta(0, 5);

        Assert.Equal(5, delta.Absolute, 0.001);
        Assert.Null(delta.Relative);
    }

    [Fact]
    public void Compare_WarmupDelta_PopulatedWhenBothPresent()
    {
        var a = CreateResult("run-a", 10, 5, 0.5, warmupTimeMs: 1000);
        var b = CreateResult("run-b", 8, 4, 0.4, warmupTimeMs: 600);

        var comparison = ResultComparer.Compare(a, b);

        Assert.NotNull(comparison.WarmupDelta);
        Assert.Equal(-400, comparison.WarmupDelta!.Absolute, 0.001);
        Assert.True(comparison.WarmupDelta.IsImproved);
    }

    [Fact]
    public void Compare_WarmupDelta_NullWhenEitherMissing()
    {
        var a = CreateResult("run-a", 10, 5, 0.5);
        var b = CreateResult("run-b", 8, 4, 0.4, warmupTimeMs: 600);

        var comparison = ResultComparer.Compare(a, b);

        Assert.Null(comparison.WarmupDelta);
    }
}
