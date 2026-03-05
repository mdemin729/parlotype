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
}
