using Parlotype.Benchmark.Reporting;
using Parlotype.Benchmark.Results;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public class CsvFormatterTests
{
    private static BenchmarkResult CreateTestResult()
    {
        return new BenchmarkResult
        {
            RunId = "test-run",
            Timestamp = DateTimeOffset.UtcNow,
            Configuration = new BenchmarkConfig
            {
                Name = "test",
                Datasets = ["ds"],
                Whisper = new WhisperConfig { Model = WhisperModelType.Base },
                Vad = new VadConfig { Enabled = true },
            },
            Environment = new EnvironmentInfo(),
            Summary = new BenchmarkSummary
            {
                TotalSamples = 2,
                AverageWer = 10,
                AverageCer = 5,
                AverageRtf = 0.5,
                ModelLoadTimeMs = 500,
                TotalProcessingTimeMs = 2000,
                PeakRamMb = 512,
            },
            Samples = [
                new SampleResult { Id = "s1", ReferenceText = "hello world", HypothesisText = "hello world", Wer = 0, Cer = 0, ProcessingTimeMs = 1000, Rtf = 0.5 },
                new SampleResult { Id = "s2", ReferenceText = "test, with comma", HypothesisText = "test with comma", Wer = 20, Cer = 10, ProcessingTimeMs = 1000, Rtf = 0.5 },
            ],
        };
    }

    [Fact]
    public void FormatResult_ContainsHeader()
    {
        var result = CreateTestResult();
        var csv = CsvFormatter.FormatResult(result);

        Assert.StartsWith("SampleId,ReferenceText,HypothesisText,WER,CER,RTF,ProcessingTimeMs", csv);
    }

    [Fact]
    public void FormatResult_EscapesCommasInFields()
    {
        var result = CreateTestResult();
        var csv = CsvFormatter.FormatResult(result);

        Assert.Contains("\"test, with comma\"", csv);
    }

    [Fact]
    public void FormatResult_HasCorrectRowCount()
    {
        var result = CreateTestResult();
        var csv = CsvFormatter.FormatResult(result);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal(3, lines.Length); // header + 2 samples
    }

    [Fact]
    public void FormatComparison_ContainsHeader()
    {
        var comparison = new ComparisonResult
        {
            RunIdA = "a",
            RunIdB = "b",
            ConfigNameA = "ca",
            ConfigNameB = "cb",
            ModelA = "Base",
            ModelB = "Small",
            RuntimeA = "cpu",
            RuntimeB = "cpu",
            WerDelta = new MetricDelta(10, 8),
            CerDelta = new MetricDelta(5, 4),
            RtfDelta = new MetricDelta(0.5, 0.4),
            ModelLoadDelta = new MetricDelta(500, 400),
            PeakRamDelta = new MetricDelta(512, 600),
            TotalTimeDelta = new MetricDelta(2000, 1800),
            SampleDeltas = [new SampleComparisonRow("s1", 10, 8, 5, 4)],
        };

        var csv = CsvFormatter.FormatComparison(comparison);

        Assert.StartsWith("SampleId,WER_A,WER_B,WER_Delta,CER_A,CER_B,CER_Delta", csv);
    }
}
