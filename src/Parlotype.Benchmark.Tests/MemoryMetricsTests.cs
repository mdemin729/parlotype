using Parlotype.Benchmark.Results;
using Parlotype.Benchmark.Configuration;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Tests;

public class MemoryMetricsTests
{
    [Fact]
    public void SampleResult_MemoryFields_DefaultToZero()
    {
        var result = new SampleResult
        {
            Id = "s1",
            ReferenceText = "ref",
            HypothesisText = "hyp",
            Wer = 10,
            Cer = 5,
            ProcessingTimeMs = 100,
            Rtf = 0.5,
        };

        Assert.Equal(0, result.RamDeltaMb);
        Assert.Equal(0, result.GcAllocatedBytes);
    }

    [Fact]
    public void SampleResult_MemoryFields_AreSettable()
    {
        var result = new SampleResult
        {
            Id = "s1",
            ReferenceText = "ref",
            HypothesisText = "hyp",
            Wer = 10,
            Cer = 5,
            ProcessingTimeMs = 100,
            Rtf = 0.5,
        };

        result.RamDeltaMb = 25.5;
        result.GcAllocatedBytes = 1024 * 1024;

        Assert.Equal(25.5, result.RamDeltaMb);
        Assert.Equal(1024 * 1024, result.GcAllocatedBytes);
    }

    [Fact]
    public void BenchmarkSummary_MemoryFields_Initialized()
    {
        var summary = new BenchmarkSummary
        {
            TotalSamples = 1,
            AverageWer = 10,
            AverageCer = 5,
            AverageRtf = 0.5,
            TotalProcessingTimeMs = 1000,
            ModelLoadTimeMs = 500,
            PeakRamMb = 512,
            AvgRamDeltaMb = 15.3,
            TotalGcAllocatedBytes = 50 * 1024 * 1024,
            GcGen0Collections = 5,
            GcGen1Collections = 2,
            GcGen2Collections = 0,
        };

        Assert.Equal(15.3, summary.AvgRamDeltaMb);
        Assert.Equal(50 * 1024 * 1024, summary.TotalGcAllocatedBytes);
        Assert.Equal(5, summary.GcGen0Collections);
        Assert.Equal(2, summary.GcGen1Collections);
        Assert.Equal(0, summary.GcGen2Collections);
    }

    [Fact]
    public void SampleResult_MemoryFields_JsonRoundtrip()
    {
        var result = new SampleResult
        {
            Id = "s1",
            ReferenceText = "ref",
            HypothesisText = "hyp",
            Wer = 10,
            Cer = 5,
            ProcessingTimeMs = 100,
            Rtf = 0.5,
        };
        result.RamDeltaMb = 12.5;
        result.GcAllocatedBytes = 2048;

        var json = System.Text.Json.JsonSerializer.Serialize(result);
        var deserialized = System.Text.Json.JsonSerializer.Deserialize<SampleResult>(json);

        Assert.NotNull(deserialized);
        Assert.Equal(12.5, deserialized!.RamDeltaMb);
        Assert.Equal(2048, deserialized.GcAllocatedBytes);
    }
}
