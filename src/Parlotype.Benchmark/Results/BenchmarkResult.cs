using System.Text.Json.Serialization;
using Parlotype.Benchmark.Configuration;

namespace Parlotype.Benchmark.Results;

/// <summary>Complete result of a benchmark run.</summary>
public sealed class BenchmarkResult
{
    [JsonPropertyName("runId")]
    public required string RunId { get; init; }

    [JsonPropertyName("timestamp")]
    public required DateTimeOffset Timestamp { get; init; }

    [JsonPropertyName("configuration")]
    public required BenchmarkConfig Configuration { get; init; }

    [JsonPropertyName("environment")]
    public required EnvironmentInfo Environment { get; init; }

    [JsonPropertyName("summary")]
    public required BenchmarkSummary Summary { get; init; }

    [JsonPropertyName("samples")]
    public required List<SampleResult> Samples { get; init; }
}

public sealed class BenchmarkSummary
{
    [JsonPropertyName("totalSamples")]
    public required int TotalSamples { get; init; }

    [JsonPropertyName("averageWer")]
    public required double AverageWer { get; init; }

    [JsonPropertyName("averageCer")]
    public required double AverageCer { get; init; }

    [JsonPropertyName("averageRtf")]
    public required double AverageRtf { get; init; }

    [JsonPropertyName("totalProcessingTimeMs")]
    public required double TotalProcessingTimeMs { get; init; }

    [JsonPropertyName("modelLoadTimeMs")]
    public required double ModelLoadTimeMs { get; init; }

    [JsonPropertyName("peakRamMb")]
    public required double PeakRamMb { get; init; }

    [JsonPropertyName("avgRamDeltaMb")]
    public double AvgRamDeltaMb { get; init; }

    [JsonPropertyName("totalGcAllocatedBytes")]
    public long TotalGcAllocatedBytes { get; init; }

    [JsonPropertyName("gcGen0Collections")]
    public int GcGen0Collections { get; init; }

    [JsonPropertyName("gcGen1Collections")]
    public int GcGen1Collections { get; init; }

    [JsonPropertyName("gcGen2Collections")]
    public int GcGen2Collections { get; init; }

    [JsonPropertyName("repetitions")]
    public int Repetitions { get; init; }

    [JsonPropertyName("werStdDev")]
    public double WerStdDev { get; init; }

    [JsonPropertyName("cerStdDev")]
    public double CerStdDev { get; init; }

    [JsonPropertyName("werCoeffOfVariation")]
    public double WerCoeffOfVariation { get; init; }
}
