using Parlotype.Benchmark.Results;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public class RepetitionTests
{
    [Fact]
    public void SampleResult_WithRepetitions_StoresAllDetails()
    {
        var reps = new List<RepetitionDetail>
        {
            new() { Repetition = 1, ProcessingTimeMs = 100, Rtf = 0.5, Wer = 10, Cer = 5, HypothesisText = "hello" },
            new() { Repetition = 2, ProcessingTimeMs = 110, Rtf = 0.55, Wer = 12, Cer = 6, HypothesisText = "helo" },
            new() { Repetition = 3, ProcessingTimeMs = 105, Rtf = 0.52, Wer = 11, Cer = 5.5, HypothesisText = "hello" },
        };

        var result = new SampleResult
        {
            Id = "s1",
            ReferenceText = "hello",
            HypothesisText = "hello",
            Wer = reps.Average(r => r.Wer),
            Cer = reps.Average(r => r.Cer),
            ProcessingTimeMs = reps.Average(r => r.ProcessingTimeMs),
            Rtf = reps.Average(r => r.Rtf),
            WerStdDev = 1.0,
            CerStdDev = 0.5,
            Repetitions = reps,
        };

        Assert.Equal(3, result.Repetitions!.Count);
        Assert.Equal(11, result.Wer, 0.01);
    }

    [Fact]
    public void SampleResult_SingleRun_RepetitionsIsNull()
    {
        var result = new SampleResult
        {
            Id = "s1",
            ReferenceText = "hello",
            HypothesisText = "hello",
            Wer = 10,
            Cer = 5,
            ProcessingTimeMs = 100,
            Rtf = 0.5,
            Repetitions = null,
        };

        Assert.Null(result.Repetitions);
        Assert.Equal(0, result.WerStdDev);
    }

    [Fact]
    public void StdDev_ComputedCorrectly_ForKnownValues()
    {
        // Values: 10, 12, 11 → mean = 11, variance = 1, stddev = 1
        var values = new[] { 10.0, 12.0, 11.0 };
        var mean = values.Average();
        var sumSquares = values.Sum(v => (v - mean) * (v - mean));
        var stddev = Math.Sqrt(sumSquares / (values.Length - 1)); // sample stddev

        Assert.Equal(1.0, stddev, 0.01);
    }

    [Fact]
    public void BenchmarkSummary_StabilityMetrics_PopulatedWhenRepetitions()
    {
        var summary = new BenchmarkSummary
        {
            TotalSamples = 2,
            AverageWer = 10,
            AverageCer = 5,
            AverageRtf = 0.5,
            TotalProcessingTimeMs = 2000,
            ModelLoadTimeMs = 500,
            PeakRamMb = 512,
            Repetitions = 3,
            WerStdDev = 1.5,
            CerStdDev = 0.8,
            WerCoeffOfVariation = 15.0,
        };

        Assert.Equal(3, summary.Repetitions);
        Assert.Equal(1.5, summary.WerStdDev);
        Assert.Equal(15.0, summary.WerCoeffOfVariation);
    }

    [Fact]
    public void RepetitionDetail_JsonRoundtrip()
    {
        var detail = new RepetitionDetail
        {
            Repetition = 1,
            ProcessingTimeMs = 100.5,
            Rtf = 0.5,
            Wer = 10.2,
            Cer = 5.1,
            HypothesisText = "test text",
        };

        var json = System.Text.Json.JsonSerializer.Serialize(detail);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<RepetitionDetail>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(1, deserialized!.Repetition);
        Assert.Equal(100.5, deserialized.ProcessingTimeMs);
        Assert.Equal("test text", deserialized.HypothesisText);
    }
}
