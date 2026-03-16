using System.Text.Json.Serialization;

namespace Parlotype.Benchmark.Reporting;

/// <summary>Result of comparing two benchmark runs.</summary>
public sealed class ComparisonResult
{
    [JsonPropertyName("runA")]
    public required string RunIdA { get; init; }

    [JsonPropertyName("runB")]
    public required string RunIdB { get; init; }

    [JsonPropertyName("configNameA")]
    public required string ConfigNameA { get; init; }

    [JsonPropertyName("configNameB")]
    public required string ConfigNameB { get; init; }

    [JsonPropertyName("modelA")]
    public required string ModelA { get; init; }

    [JsonPropertyName("modelB")]
    public required string ModelB { get; init; }

    [JsonPropertyName("runtimeA")]
    public required string RuntimeA { get; init; }

    [JsonPropertyName("runtimeB")]
    public required string RuntimeB { get; init; }

    [JsonPropertyName("werDelta")]
    public required MetricDelta WerDelta { get; init; }

    [JsonPropertyName("cerDelta")]
    public required MetricDelta CerDelta { get; init; }

    [JsonPropertyName("rtfDelta")]
    public required MetricDelta RtfDelta { get; init; }

    [JsonPropertyName("modelLoadDelta")]
    public required MetricDelta ModelLoadDelta { get; init; }

    [JsonPropertyName("peakRamDelta")]
    public required MetricDelta PeakRamDelta { get; init; }

    [JsonPropertyName("totalTimeDelta")]
    public required MetricDelta TotalTimeDelta { get; init; }

    [JsonPropertyName("sampleDeltas")]
    public List<SampleComparisonRow> SampleDeltas { get; init; } = [];
}

/// <summary>Delta between two metric values.</summary>
public sealed record MetricDelta(
    [property: JsonPropertyName("valueA")] double ValueA,
    [property: JsonPropertyName("valueB")] double ValueB,
    [property: JsonPropertyName("lowerIsBetter")] bool LowerIsBetter = true)
{
    [JsonPropertyName("absolute")]
    public double Absolute => ValueB - ValueA;

    [JsonPropertyName("relative")]
    public double? Relative => ValueA != 0 ? (ValueB - ValueA) / ValueA * 100 : null;

    [JsonPropertyName("isImproved")]
    public bool IsImproved => LowerIsBetter ? ValueB < ValueA : ValueB > ValueA;
}

/// <summary>Per-sample comparison row.</summary>
public sealed record SampleComparisonRow(
    [property: JsonPropertyName("sampleId")] string SampleId,
    [property: JsonPropertyName("werA")] double WerA,
    [property: JsonPropertyName("werB")] double WerB,
    [property: JsonPropertyName("cerA")] double CerA,
    [property: JsonPropertyName("cerB")] double CerB);
