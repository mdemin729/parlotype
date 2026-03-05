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
