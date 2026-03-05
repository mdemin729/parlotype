using System.Globalization;
using System.Text;
using Parlotype.Benchmark.Results;

namespace Parlotype.Benchmark.Reporting;

/// <summary>Formats benchmark results and comparisons as GitHub-flavored Markdown tables.</summary>
public static class MarkdownFormatter
{
    /// <summary>Formats a single benchmark run as a Markdown report.</summary>
    public static string FormatResult(BenchmarkResult result)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Benchmark: {result.Configuration.Name}");
        sb.AppendLine();
        sb.AppendLine($"**Run ID:** {result.RunId}  ");
        sb.AppendLine($"**Timestamp:** {result.Timestamp:yyyy-MM-dd HH:mm:ss UTC}  ");
        sb.AppendLine($"**Model:** {result.Configuration.Whisper.Model}  ");
        sb.AppendLine($"**Language:** {result.Configuration.Whisper.Language}  ");
        sb.AppendLine($"**VAD:** {(result.Configuration.Vad.Enabled ? "Enabled" : "Disabled")}  ");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Value |");
        sb.AppendLine("|--------|------:|");
        sb.AppendLine($"| Samples | {result.Summary.TotalSamples} |");
        sb.AppendLine($"| Avg WER | {Fmt(result.Summary.AverageWer)}% |");
        sb.AppendLine($"| Avg CER | {Fmt(result.Summary.AverageCer)}% |");
        sb.AppendLine($"| Avg RTF | {result.Summary.AverageRtf.ToString("F3", CultureInfo.InvariantCulture)} |");
        sb.AppendLine($"| Model Load | {Fmt(result.Summary.ModelLoadTimeMs, "F0")} ms |");
        sb.AppendLine($"| Total Time | {Fmt(result.Summary.TotalProcessingTimeMs, "F0")} ms |");
        sb.AppendLine($"| Peak RAM | {Fmt(result.Summary.PeakRamMb, "F0")} MB |");
        sb.AppendLine();

        sb.AppendLine("## Per-Sample Results");
        sb.AppendLine();
        sb.AppendLine("| Sample | WER % | CER % | RTF | Time (ms) |");
        sb.AppendLine("|--------|------:|------:|----:|----------:|");

        foreach (var sample in result.Samples)
        {
            sb.AppendLine($"| {sample.Id} | {Fmt(sample.Wer)} | {Fmt(sample.Cer)} | {sample.Rtf.ToString("F3", CultureInfo.InvariantCulture)} | {Fmt(sample.ProcessingTimeMs, "F0")} |");
        }

        return sb.ToString();
    }

    /// <summary>Formats a comparison between two runs as a Markdown report.</summary>
    public static string FormatComparison(ComparisonResult comparison)
    {
        var sb = new StringBuilder();

        sb.AppendLine($"# Comparison: {comparison.ConfigNameA} vs {comparison.ConfigNameB}");
        sb.AppendLine();
        sb.AppendLine($"**Run A:** {comparison.RunIdA} ({comparison.ModelA})  ");
        sb.AppendLine($"**Run B:** {comparison.RunIdB} ({comparison.ModelB})  ");
        sb.AppendLine();

        sb.AppendLine("## Summary");
        sb.AppendLine();
        sb.AppendLine("| Metric | Run A | Run B | Δ | |");
        sb.AppendLine("|--------|------:|------:|--:|-|");

        AppendDeltaRow(sb, "Avg WER", comparison.WerDelta, "%");
        AppendDeltaRow(sb, "Avg CER", comparison.CerDelta, "%");
        AppendDeltaRow(sb, "Avg RTF", comparison.RtfDelta, "", "F3");
        AppendDeltaRow(sb, "Model Load", comparison.ModelLoadDelta, " ms", "F0");
        AppendDeltaRow(sb, "Peak RAM", comparison.PeakRamDelta, " MB", "F0");
        AppendDeltaRow(sb, "Total Time", comparison.TotalTimeDelta, " ms", "F0");

        if (comparison.SampleDeltas.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## Per-Sample Comparison");
            sb.AppendLine();
            sb.AppendLine("| Sample | WER A | WER B | Δ WER | CER A | CER B | Δ CER |");
            sb.AppendLine("|--------|------:|------:|------:|------:|------:|------:|");

            foreach (var row in comparison.SampleDeltas)
            {
                var werDelta = row.WerB - row.WerA;
                var cerDelta = row.CerB - row.CerA;
                sb.AppendLine($"| {row.SampleId} | {Fmt(row.WerA)} | {Fmt(row.WerB)} | {FmtDelta(werDelta)} | {Fmt(row.CerA)} | {Fmt(row.CerB)} | {FmtDelta(cerDelta)} |");
            }
        }

        return sb.ToString();
    }

    private static void AppendDeltaRow(StringBuilder sb, string label, MetricDelta delta, string suffix, string format = "F1")
    {
        var indicator = delta.IsImproved ? "✅" : (delta.Absolute == 0 ? "—" : "⚠️");
        var deltaStr = delta.Absolute >= 0
            ? $"+{delta.Absolute.ToString(format, CultureInfo.InvariantCulture)}{suffix}"
            : $"{delta.Absolute.ToString(format, CultureInfo.InvariantCulture)}{suffix}";

        sb.AppendLine($"| {label} | {delta.ValueA.ToString(format, CultureInfo.InvariantCulture)}{suffix} | {delta.ValueB.ToString(format, CultureInfo.InvariantCulture)}{suffix} | {deltaStr} | {indicator} |");
    }

    private static string Fmt(double value, string format = "F1")
        => value.ToString(format, CultureInfo.InvariantCulture);

    private static string FmtDelta(double delta)
        => delta >= 0 ? $"+{delta.ToString("F1", CultureInfo.InvariantCulture)}" : delta.ToString("F1", CultureInfo.InvariantCulture);
}
