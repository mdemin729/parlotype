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
        AddDeltaRow(table, "Runtime", comparison.RuntimeA, comparison.RuntimeB);
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
            .AddColumn("[bold]Runtime[/]")
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
                Markup.Escape(run.Runtime),
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

        table.AddRow("Engine", config.EngineName);
        table.AddRow("Model", config.ModelDisplayName);
        table.AddRow("Runtime", result.Environment.WhisperRuntime);
        table.AddRow("Language", config.IsLlamaCpp ? "en (llama.cpp)" : config.EffectiveWhisper.Language);
        table.AddRow("VAD", config.Vad.Enabled ? "Enabled" : "Disabled");
        table.AddRow("Samples", summary.TotalSamples.ToString());
        table.AddRow("Avg WER", $"{summary.AverageWer:F1}%");
        table.AddRow("Avg CER", $"{summary.AverageCer:F1}%");
        table.AddRow("Avg RTF", $"{summary.AverageRtf:F3}");
        table.AddRow("Model Load", $"{summary.ModelLoadTimeMs:F0} ms");
        table.AddRow("Total Time", $"{summary.TotalProcessingTimeMs:F0} ms");
        table.AddRow("Peak RAM", $"{summary.PeakRamMb:F0} MB");

        if (summary.Repetitions > 1)
        {
            table.AddRow("Repetitions", summary.Repetitions.ToString());
            table.AddRow("WER σ", $"{summary.WerStdDev:F2}%");
            table.AddRow("CER σ", $"{summary.CerStdDev:F2}%");
            table.AddRow("WER CoV", $"{summary.WerCoeffOfVariation:F1}%");
        }

        if (summary.AvgRamDeltaMb > 0 || summary.TotalGcAllocatedBytes > 0)
        {
            table.AddRow("Avg RAM Δ", $"{summary.AvgRamDeltaMb:F1} MB");
            table.AddRow("GC Allocated", FormatBytes(summary.TotalGcAllocatedBytes));
            table.AddRow("GC Collections", $"Gen0: {summary.GcGen0Collections}, Gen1: {summary.GcGen1Collections}, Gen2: {summary.GcGen2Collections}");
        }

        AnsiConsole.Write(table);
    }

    private static void DisplaySampleTable(BenchmarkResult result)
    {
        var hasReps = result.Summary.Repetitions > 1;

        var table = new Table()
            .Border(TableBorder.Rounded)
            .Title("[bold]Per-Sample Results[/]")
            .AddColumn("[bold]Sample[/]")
            .AddColumn("[bold]WER %[/]", c => c.RightAligned())
            .AddColumn("[bold]CER %[/]", c => c.RightAligned());

        if (hasReps)
        {
            table.AddColumn("[bold]WER σ[/]", c => c.RightAligned());
            table.AddColumn("[bold]CER σ[/]", c => c.RightAligned());
        }

        table.AddColumn("[bold]RTF[/]", c => c.RightAligned())
             .AddColumn("[bold]Time (ms)[/]", c => c.RightAligned());

        foreach (var sample in result.Samples)
        {
            var werColor = sample.Wer switch
            {
                <= 5 => "green",
                <= 15 => "yellow",
                _ => "red"
            };

            var columns = new List<string>
            {
                Markup.Escape(sample.Id),
                $"[{werColor}]{sample.Wer:F1}[/]",
                $"{sample.Cer:F1}",
            };

            if (hasReps)
            {
                columns.Add($"{sample.WerStdDev:F2}");
                columns.Add($"{sample.CerStdDev:F2}");
            }

            columns.Add($"{sample.Rtf:F3}");
            columns.Add($"{sample.ProcessingTimeMs:F0}");

            table.AddRow(columns.ToArray());
        }

        AnsiConsole.Write(table);
    }

    /// <summary>Displays a summary table comparing all sweep configuration results.</summary>
    public static void DisplaySweepSummary(List<BenchmarkResult> results)
    {
        if (results.Count == 0)
            return;

        AnsiConsole.WriteLine();
        AnsiConsole.Write(new Rule("[bold blue]Sweep Summary[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        var table = new Table()
            .Border(TableBorder.Rounded)
            .AddColumn("[bold]Config[/]")
            .AddColumn("[bold]Model[/]")
            .AddColumn("[bold]Beam[/]", c => c.RightAligned())
            .AddColumn("[bold]VAD[/]")
            .AddColumn("[bold]WER %[/]", c => c.RightAligned())
            .AddColumn("[bold]CER %[/]", c => c.RightAligned())
            .AddColumn("[bold]RTF[/]", c => c.RightAligned())
            .AddColumn("[bold]Time (ms)[/]", c => c.RightAligned())
            .AddColumn("[bold]RAM (MB)[/]", c => c.RightAligned());

        // Find best WER for highlighting
        var bestWer = results.Min(r => r.Summary.AverageWer);

        foreach (var result in results)
        {
            var werColor = result.Summary.AverageWer == bestWer ? "green" : "white";

            table.AddRow(
                Markup.Escape(result.Configuration.Name),
                result.Configuration.ModelDisplayName,
                result.Configuration.IsLlamaCpp ? "-" : result.Configuration.EffectiveWhisper.BeamSize.ToString(),
                result.Configuration.Vad.Enabled ? "✓" : "✗",
                $"[{werColor}]{result.Summary.AverageWer:F1}[/]",
                $"{result.Summary.AverageCer:F1}",
                $"{result.Summary.AverageRtf:F3}",
                $"{result.Summary.TotalProcessingTimeMs:F0}",
                $"{result.Summary.PeakRamMb:F0}");
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
        AnsiConsole.MarkupLine($"  [grey]Whisper Runtime:[/] {Markup.Escape(env.WhisperRuntime)}");
    }

    private static string FormatBytes(long bytes)
    {
        return bytes switch
        {
            >= 1024 * 1024 * 1024 => $"{bytes / (1024.0 * 1024.0 * 1024.0):F1} GB",
            >= 1024 * 1024 => $"{bytes / (1024.0 * 1024.0):F1} MB",
            >= 1024 => $"{bytes / 1024.0:F1} KB",
            _ => $"{bytes} B",
        };
    }
}
