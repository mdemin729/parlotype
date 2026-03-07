using Parlotype.Benchmark.Results;
using Spectre.Console;

namespace Parlotype.Benchmark.Reporting;

/// <summary>Displays benchmark results as rich console tables using Spectre.Console.</summary>
public static class ConsoleReporter
{
    /// <summary>Displays the full benchmark result with summary and per-sample breakdown.</summary>
    public static void DisplayResult(BenchmarkResult result)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold blue]Benchmark Result: {Markup.Escape(result.Configuration.Name)}[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        DisplaySummaryTable(result);
        AnsiConsole.WriteLine();
        DisplaySampleTable(result);
        AnsiConsole.WriteLine();
        DisplayEnvironment(result.Environment);
    }

    /// <summary>Displays a comparison between two benchmark runs.</summary>
    public static void DisplayComparison(ComparisonResult comparison)
    {
        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule($"[bold blue]Comparison: {Markup.Escape(comparison.ConfigNameA)} vs {Markup.Escape(comparison.ConfigNameB)}[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Summary Comparison[/]")
            .AddColumn("[bold]Metric[/]")
            .AddColumn("[bold]Run A[/]", c => c.RightAligned())
            .AddColumn("[bold]Run B[/]", c => c.RightAligned())
            .AddColumn("[bold]Δ[/]", c => c.RightAligned())
            .AddColumn("[bold][/]");

        AddDeltaRow(table, "Model", comparison.ModelA, comparison.ModelB);
        AddMetricRow(table, "Avg WER", comparison.WerDelta, "%");
        AddMetricRow(table, "Avg CER", comparison.CerDelta, "%");
        AddMetricRow(table, "Avg RTF", comparison.RtfDelta, "", "F3");
        AddMetricRow(table, "Model Load", comparison.ModelLoadDelta, " ms", "F0");
        AddMetricRow(table, "Peak RAM", comparison.PeakRamDelta, " MB", "F0");
        AddMetricRow(table, "Total Time", comparison.TotalTimeDelta, " ms", "F0");

        AnsiConsole.Write(table);

        if (comparison.SampleDeltas.Count > 0)
        {
            AnsiConsole.WriteLine();
            DisplaySampleComparison(comparison);
        }
    }

    /// <summary>Displays a list of historical runs.</summary>
    public static void DisplayRunList(List<RunSummaryRow> runs)
    {
        if (runs.Count == 0)
        {
            AnsiConsole.MarkupLine("[yellow]No benchmark runs found.[/]");
            return;
        }

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Benchmark Runs[/]")
            .AddColumn("[bold]Run ID[/]")
            .AddColumn("[bold]Config[/]")
            .AddColumn("[bold]Model[/]")
            .AddColumn("[bold]Samples[/]", c => c.RightAligned())
            .AddColumn("[bold]WER %[/]", c => c.RightAligned())
            .AddColumn("[bold]CER %[/]", c => c.RightAligned())
            .AddColumn("[bold]RTF[/]", c => c.RightAligned())
            .AddColumn("[bold]Date[/]");

        foreach (var run in runs)
        {
            var werColor = run.AvgWer switch
            {
                <= 5 => "green",
                <= 15 => "yellow",
                _ => "red",
            };

            table.AddRow(
                Markup.Escape(run.RunId),
                Markup.Escape(run.ConfigName),
                Markup.Escape(run.Model),
                run.TotalSamples.ToString(),
                $"[{werColor}]{run.AvgWer:F1}[/]",
                $"{run.AvgCer:F1}",
                $"{run.AvgRtf:F3}",
                run.Timestamp.LocalDateTime.ToString("yyyy-MM-dd HH:mm"));
        }

        AnsiConsole.Write(table);
    }

    private static void DisplaySampleComparison(ComparisonResult comparison)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Per-Sample Comparison[/]")
            .AddColumn("[bold]Sample[/]")
            .AddColumn("[bold]WER A[/]", c => c.RightAligned())
            .AddColumn("[bold]WER B[/]", c => c.RightAligned())
            .AddColumn("[bold]Δ WER[/]", c => c.RightAligned())
            .AddColumn("[bold]CER A[/]", c => c.RightAligned())
            .AddColumn("[bold]CER B[/]", c => c.RightAligned())
            .AddColumn("[bold]Δ CER[/]", c => c.RightAligned());

        foreach (var row in comparison.SampleDeltas)
        {
            var werDelta = row.WerB - row.WerA;
            var cerDelta = row.CerB - row.CerA;

            table.AddRow(
                Markup.Escape(row.SampleId),
                $"{row.WerA:F1}",
                $"{row.WerB:F1}",
                FormatDelta(werDelta),
                $"{row.CerA:F1}",
                $"{row.CerB:F1}",
                FormatDelta(cerDelta));
        }

        AnsiConsole.Write(table);
    }

    private static void AddMetricRow(Table table, string label, MetricDelta delta, string suffix, string format = "F1")
    {
        var indicator = delta.IsImproved ? "[green]✅[/]" : (delta.Absolute == 0 ? "[grey]—[/]" : "[red]⚠️[/]");
        var deltaStr = FormatDelta(delta.Absolute, format, suffix);
        table.AddRow(
            label,
            $"{delta.ValueA.ToString(format)}{suffix}",
            $"{delta.ValueB.ToString(format)}{suffix}",
            deltaStr,
            indicator);
    }

    private static void AddDeltaRow(Table table, string label, string valueA, string valueB)
    {
        var indicator = valueA == valueB ? "[grey]—[/]" : "[blue]≠[/]";
        table.AddRow(label, Markup.Escape(valueA), Markup.Escape(valueB), "", indicator);
    }

    private static string FormatDelta(double delta, string format = "F1", string suffix = "")
    {
        var color = delta < 0 ? "green" : (delta == 0 ? "grey" : "red");
        var sign = delta >= 0 ? "+" : "";
        return $"[{color}]{sign}{delta.ToString(format)}{suffix}[/]";
    }

    private static void DisplaySummaryTable(BenchmarkResult result)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Summary[/]")
            .AddColumn("[bold]Metric[/]")
            .AddColumn("[bold]Value[/]");

        var summary = result.Summary;
        var config = result.Configuration;

        table.AddRow("Model", config.Whisper.Model.ToString());
        table.AddRow("Language", config.Whisper.Language);
        table.AddRow("VAD", config.Vad.Enabled ? "Enabled" : "Disabled");
        table.AddRow("Samples", summary.TotalSamples.ToString());
        table.AddRow("Avg WER", $"{summary.AverageWer:F1}%");
        table.AddRow("Avg CER", $"{summary.AverageCer:F1}%");
        table.AddRow("Avg RTF", $"{summary.AverageRtf:F3}");
        table.AddRow("Model Load", $"{summary.ModelLoadTimeMs:F0} ms");
        table.AddRow("Total Time", $"{summary.TotalProcessingTimeMs:F0} ms");
        table.AddRow("Peak RAM", $"{summary.PeakRamMb:F0} MB");

        AnsiConsole.Write(table);
    }

    private static void DisplaySampleTable(BenchmarkResult result)
    {
        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Per-Sample Results[/]")
            .AddColumn("[bold]Sample[/]")
            .AddColumn("[bold]WER %[/]", c => c.RightAligned())
            .AddColumn("[bold]CER %[/]", c => c.RightAligned())
            .AddColumn("[bold]RTF[/]", c => c.RightAligned())
            .AddColumn("[bold]Time (ms)[/]", c => c.RightAligned());

        foreach (var sample in result.Samples)
        {
            var werColor = sample.Wer switch
            {
                <= 5 => "green",
                <= 15 => "yellow",
                _ => "red"
            };

            table.AddRow(
                Markup.Escape(sample.Id),
                $"[{werColor}]{sample.Wer:F1}[/]",
                $"{sample.Cer:F1}",
                $"{sample.Rtf:F3}",
                $"{sample.ProcessingTimeMs:F0}");
        }

        AnsiConsole.Write(table);
    }

    private static void DisplayEnvironment(EnvironmentInfo env)
    {
        AnsiConsole.Write(new Rule("[grey]Environment[/]").RuleStyle("grey"));
        AnsiConsole.MarkupLine($"  [grey]OS:[/] {Markup.Escape(env.Os)}");
        AnsiConsole.MarkupLine($"  [grey]Arch:[/] {Markup.Escape(env.Architecture)}");
        AnsiConsole.MarkupLine($"  [grey].NET:[/] {Markup.Escape(env.DotnetVersion)}");
        AnsiConsole.MarkupLine($"  [grey]CPUs:[/] {env.ProcessorCount}");
    }
}
