using Parlotype.Benchmark.Reporting;
using Parlotype.Benchmark.Results;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public class MarkdownFormatterTests
{
    private static BenchmarkResult CreateTestResult()
    {
        return new BenchmarkResult
        {
            RunId = "test-run",
            Timestamp = new DateTimeOffset(2025, 3, 4, 12, 0, 0, TimeSpan.Zero),
            Configuration = new BenchmarkConfig
            {
                Name = "smoke-test",
                Datasets = ["ds"],
                Whisper = new WhisperConfig { Model = WhisperModelType.Base, Language = "en" },
                Vad = new VadConfig { Enabled = true },
            },
            Environment = new EnvironmentInfo(),
            Summary = new BenchmarkSummary
            {
                TotalSamples = 1,
                AverageWer = 10.5,
                AverageCer = 5.2,
                AverageRtf = 0.456,
                ModelLoadTimeMs = 500,
                TotalProcessingTimeMs = 2000,
                PeakRamMb = 512,
            },
            Samples = [
                new SampleResult { Id = "s1", ReferenceText = "hello", HypothesisText = "hello", Wer = 10.5, Cer = 5.2, ProcessingTimeMs = 2000, Rtf = 0.456 },
            ],
        };
    }

    [Fact]
    public void FormatResult_ContainsTitle()
    {
        var result = CreateTestResult();
        var md = MarkdownFormatter.FormatResult(result);

        Assert.Contains("# Benchmark: smoke-test", md);
    }

    [Fact]
    public void FormatResult_ContainsSummaryTable()
    {
        var result = CreateTestResult();
        var md = MarkdownFormatter.FormatResult(result);

        Assert.Contains("## Summary", md);
        Assert.Contains("| Avg WER | 10.5% |", md);
    }

    [Fact]
    public void FormatResult_ContainsPerSampleTable()
    {
        var result = CreateTestResult();
        var md = MarkdownFormatter.FormatResult(result);

        Assert.Contains("## Per-Sample Results", md);
        Assert.Contains("| s1 |", md);
    }

    [Fact]
    public void FormatComparison_ContainsComparisonTitle()
    {
        var comparison = new ComparisonResult
        {
            RunIdA = "a",
            RunIdB = "b",
            ConfigNameA = "baseline",
            ConfigNameB = "improved",
            ModelA = "Base",
            ModelB = "Base",
            RuntimeA = "cpu",
            RuntimeB = "cpu",
            WerDelta = new MetricDelta(10, 8),
            CerDelta = new MetricDelta(5, 4),
            RtfDelta = new MetricDelta(0.5, 0.4),
            ModelLoadDelta = new MetricDelta(500, 400),
            PeakRamDelta = new MetricDelta(512, 600),
            TotalTimeDelta = new MetricDelta(2000, 1800),
            SampleDeltas = [],
        };

        var md = MarkdownFormatter.FormatComparison(comparison);

        Assert.Contains("# Comparison: baseline vs improved", md);
    }

    [Fact]
    public void FormatComparison_ContainsIndicators()
    {
        var comparison = new ComparisonResult
        {
            RunIdA = "a",
            RunIdB = "b",
            ConfigNameA = "baseline",
            ConfigNameB = "improved",
            ModelA = "Base",
            ModelB = "Base",
            RuntimeA = "cpu",
            RuntimeB = "cpu",
            WerDelta = new MetricDelta(10, 8),
            CerDelta = new MetricDelta(5, 4),
            RtfDelta = new MetricDelta(0.5, 0.4),
            ModelLoadDelta = new MetricDelta(500, 400),
            PeakRamDelta = new MetricDelta(512, 600),
            TotalTimeDelta = new MetricDelta(2000, 1800),
            SampleDeltas = [],
        };

        var md = MarkdownFormatter.FormatComparison(comparison);

        Assert.Contains("✅", md); // WER improved
        Assert.Contains("⚠️", md); // Peak RAM regressed
    }
}
