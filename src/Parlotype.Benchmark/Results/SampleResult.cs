using System.Text.Json.Serialization;

namespace Parlotype.Benchmark.Results;

/// <summary>Metrics result for a single audio sample.</summary>
public sealed class SampleResult
{
    [JsonPropertyName("id")]
    public required string Id { get; init; }

    [JsonPropertyName("referenceText")]
    public required string ReferenceText { get; init; }

    [JsonPropertyName("hypothesisText")]
    public required string HypothesisText { get; init; }

    [JsonPropertyName("wer")]
    public required double Wer { get; init; }

    [JsonPropertyName("cer")]
    public required double Cer { get; init; }

    [JsonPropertyName("processingTimeMs")]
    public required double ProcessingTimeMs { get; init; }

    [JsonPropertyName("rtf")]
    public required double Rtf { get; init; }

    [JsonPropertyName("ramDeltaMb")]
    public double RamDeltaMb { get; set; }

    [JsonPropertyName("gcAllocatedBytes")]
    public long GcAllocatedBytes { get; set; }

    [JsonPropertyName("repetitions")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<RepetitionDetail>? Repetitions { get; init; }

    [JsonPropertyName("werStdDev")]
    public double WerStdDev { get; init; }

    [JsonPropertyName("cerStdDev")]
    public double CerStdDev { get; init; }
}
