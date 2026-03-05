using System.Text.Json.Serialization;

namespace Parlotype.Benchmark.Results;

/// <summary>Metrics from a single repetition of a sample.</summary>
public sealed class RepetitionDetail
{
    [JsonPropertyName("repetition")]
    public required int Repetition { get; init; }

    [JsonPropertyName("processingTimeMs")]
    public required double ProcessingTimeMs { get; init; }

    [JsonPropertyName("rtf")]
    public required double Rtf { get; init; }

    [JsonPropertyName("wer")]
    public required double Wer { get; init; }

    [JsonPropertyName("cer")]
    public required double Cer { get; init; }

    [JsonPropertyName("hypothesisText")]
    public required string HypothesisText { get; init; }
}
