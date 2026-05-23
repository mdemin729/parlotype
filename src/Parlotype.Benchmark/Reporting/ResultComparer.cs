using Parlotype.Benchmark.Results;

namespace Parlotype.Benchmark.Reporting;

/// <summary>Computes deltas between two benchmark results for comparison.</summary>
public static class ResultComparer
{
    /// <summary>Compares two benchmark runs and produces a structured comparison.</summary>
    public static ComparisonResult Compare(BenchmarkResult runA, BenchmarkResult runB)
    {
        var sampleDeltas = ComputeSampleDeltas(runA, runB);

        return new ComparisonResult
        {
            RunIdA = runA.RunId,
            RunIdB = runB.RunId,
            ConfigNameA = runA.Configuration.Name,
            ConfigNameB = runB.Configuration.Name,
            ModelA = runA.Configuration.ModelDisplayName,
            ModelB = runB.Configuration.ModelDisplayName,
            RuntimeA = runA.Environment.WhisperRuntime,
            RuntimeB = runB.Environment.WhisperRuntime,
            WerDelta = new MetricDelta(runA.Summary.AverageWer, runB.Summary.AverageWer),
            CerDelta = new MetricDelta(runA.Summary.AverageCer, runB.Summary.AverageCer),
            RtfDelta = new MetricDelta(runA.Summary.AverageRtf, runB.Summary.AverageRtf),
            ModelLoadDelta = new MetricDelta(runA.Summary.ModelLoadTimeMs, runB.Summary.ModelLoadTimeMs),
            WarmupDelta = runA.Summary.WarmupTimeMs is { } warmupA && runB.Summary.WarmupTimeMs is { } warmupB
                ? new MetricDelta(warmupA, warmupB)
                : null,
            PeakRamDelta = new MetricDelta(runA.Summary.PeakRamMb, runB.Summary.PeakRamMb),
            TotalTimeDelta = new MetricDelta(runA.Summary.TotalProcessingTimeMs, runB.Summary.TotalProcessingTimeMs),
            SampleDeltas = sampleDeltas,
        };
    }

    private static List<SampleComparisonRow> ComputeSampleDeltas(BenchmarkResult runA, BenchmarkResult runB)
    {
        var samplesB = runB.Samples.ToDictionary(s => s.Id);
        var deltas = new List<SampleComparisonRow>();

        foreach (var sampleA in runA.Samples)
        {
            if (samplesB.TryGetValue(sampleA.Id, out var sampleB))
            {
                deltas.Add(new SampleComparisonRow(
                    sampleA.Id, sampleA.Wer, sampleB.Wer, sampleA.Cer, sampleB.Cer));
            }
        }

        return deltas;
    }
}
