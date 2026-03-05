using System.Globalization;
using System.Text;
using Parlotype.Benchmark.Results;

namespace Parlotype.Benchmark.Reporting;

/// <summary>Formats benchmark results and comparisons as RFC 4180 CSV.</summary>
public static class CsvFormatter
{
    /// <summary>Formats a single benchmark run's per-sample results as CSV.</summary>
    public static string FormatResult(BenchmarkResult result)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SampleId,ReferenceText,HypothesisText,WER,CER,RTF,ProcessingTimeMs");

        foreach (var sample in result.Samples)
        {
            sb.Append(Escape(sample.Id)).Append(',');
            sb.Append(Escape(sample.ReferenceText)).Append(',');
            sb.Append(Escape(sample.HypothesisText)).Append(',');
            sb.Append(sample.Wer.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(sample.Cer.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(sample.Rtf.ToString("F4", CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine(sample.ProcessingTimeMs.ToString("F0", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>Formats a comparison between two runs as CSV.</summary>
    public static string FormatComparison(ComparisonResult comparison)
    {
        var sb = new StringBuilder();
        sb.AppendLine("SampleId,WER_A,WER_B,WER_Delta,CER_A,CER_B,CER_Delta");

        foreach (var row in comparison.SampleDeltas)
        {
            sb.Append(Escape(row.SampleId)).Append(',');
            sb.Append(row.WerA.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.WerB.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append((row.WerB - row.WerA).ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.CerA.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.Append(row.CerB.ToString("F2", CultureInfo.InvariantCulture)).Append(',');
            sb.AppendLine((row.CerB - row.CerA).ToString("F2", CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    /// <summary>Escapes a CSV field per RFC 4180.</summary>
    private static string Escape(string field)
    {
        if (field.Contains(',') || field.Contains('"') || field.Contains('\n') || field.Contains('\r'))
            return $"\"{field.Replace("\"", "\"\"")}\"";
        return field;
    }
}
