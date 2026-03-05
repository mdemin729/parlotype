namespace Parlotype.Benchmark.Metrics;

/// <summary>Computed metrics for a single sample transcription.</summary>
public sealed record MetricsResult(
    double Wer,
    double Cer,
    double ProcessingTimeMs,
    double Rtf);
