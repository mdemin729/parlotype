using Parlotype.Benchmark.Reporting;
using Parlotype.Benchmark.Results;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public class CheckCommandTests
{
    private static BenchmarkResult CreateResult(string runId, double avgWer, double avgCer, double avgRtf)
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
                TotalSamples = 1,
                AverageWer = avgWer,
                AverageCer = avgCer,
                AverageRtf = avgRtf,
                ModelLoadTimeMs = 500,
                TotalProcessingTimeMs = 1000,
                PeakRamMb = 512,
            },
            Samples = [
                new SampleResult { Id = "s1", ReferenceText = "ref", HypothesisText = "hyp", Wer = avgWer, Cer = avgCer, ProcessingTimeMs = 1000, Rtf = avgRtf },
            ],
        };
    }

    [Fact]
    public void Comparison_WithinThresholds_NoFailures()
    {
        var baseline = CreateResult("baseline", 10.0, 5.0, 0.5);
        var current = CreateResult("current", 11.0, 5.5, 0.55);

        var comparison = ResultComparer.Compare(baseline, current);

        // Thresholds: WER +2.0, CER +1.0, RTF +0.1
        var werExceeded = comparison.WerDelta.Absolute > 2.0;
        var cerExceeded = comparison.CerDelta.Absolute > 1.0;
        var rtfExceeded = comparison.RtfDelta.Absolute > 0.1;

        Assert.False(werExceeded); // +1.0, threshold 2.0
        Assert.False(cerExceeded); // +0.5, threshold 1.0
        Assert.False(rtfExceeded); // +0.05, threshold 0.1
    }

    [Fact]
    public void Comparison_WerExceedsThreshold_Detected()
    {
        var baseline = CreateResult("baseline", 10.0, 5.0, 0.5);
        var current = CreateResult("current", 13.0, 5.5, 0.55);

        var comparison = ResultComparer.Compare(baseline, current);

        Assert.True(comparison.WerDelta.Absolute > 2.0); // +3.0 > threshold 2.0
    }

    [Fact]
    public void Comparison_CerExceedsThreshold_Detected()
    {
        var baseline = CreateResult("baseline", 10.0, 5.0, 0.5);
        var current = CreateResult("current", 11.0, 7.0, 0.55);

        var comparison = ResultComparer.Compare(baseline, current);

        Assert.True(comparison.CerDelta.Absolute > 1.0); // +2.0 > threshold 1.0
    }

    [Fact]
    public void Comparison_Improved_NoRegressionDetected()
    {
        var baseline = CreateResult("baseline", 10.0, 5.0, 0.5);
        var current = CreateResult("current", 8.0, 4.0, 0.4);

        var comparison = ResultComparer.Compare(baseline, current);

        Assert.True(comparison.WerDelta.Absolute < 0); // Improved, negative delta
        Assert.True(comparison.CerDelta.Absolute < 0);
        Assert.True(comparison.RtfDelta.Absolute < 0);
    }

    [Fact]
    public void Comparison_ExactlyAtThreshold_NotExceeded()
    {
        var baseline = CreateResult("baseline", 10.0, 5.0, 0.5);
        var current = CreateResult("current", 12.0, 6.0, 0.6);

        var comparison = ResultComparer.Compare(baseline, current);

        // Exactly at threshold (not exceeding)
        Assert.False(comparison.WerDelta.Absolute > 2.0); // +2.0 == threshold, not >
        Assert.False(comparison.CerDelta.Absolute > 1.0); // +1.0 == threshold, not >
        Assert.False(comparison.RtfDelta.Absolute > 0.1); // +0.1 == threshold, not >
    }
}
