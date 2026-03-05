using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Benchmark.Configuration;
using Parlotype.Benchmark.Pipeline;
using Parlotype.Benchmark.Reporting;
using Parlotype.Benchmark.Results;
using Parlotype.Core.Audio;
using Parlotype.Core.Speech;
using Parlotype.Platform;
using Spectre.Console;
using ZLogger;

var rootCommand = new RootCommand("Parlotype Benchmark — Speech recognition quality evaluation tool");

var configOption = new Option<FileInfo>("--config")
{
    Description = "Path to benchmark configuration JSON file",
    Required = true,
};

var datasetsOption = new Option<DirectoryInfo>("--datasets")
{
    Description = "Root directory containing dataset folders",
    Required = true,
};

var outputOption = new Option<DirectoryInfo>("--output")
{
    Description = "Output directory for result files",
    DefaultValueFactory = _ => new DirectoryInfo("./results"),
};

var verboseOption = new Option<bool>("--verbose", "-v")
{
    Description = "Enable verbose logging",
};

var tagsOption = new Option<string?>("--tags")
{
    Description = "Comma-separated tags to filter samples (AND logic, e.g. 'clean,short')",
};

var samplesOption = new Option<string?>("--samples")
{
    Description = "Comma-separated sample IDs to include (e.g. 'kennedy,one-small-step')",
};

var runCommand = new Command("run", "Execute a benchmark run")
{
    configOption,
    datasetsOption,
    outputOption,
    verboseOption,
    tagsOption,
    samplesOption,
};

runCommand.SetAction(async parseResult =>
{
    var configFile = parseResult.GetValue(configOption)!;
    var datasetsDir = parseResult.GetValue(datasetsOption)!;
    var outputDir = parseResult.GetValue(outputOption)!;
    var verbose = parseResult.GetValue(verboseOption);
    var tagsFilter = parseResult.GetValue(tagsOption);
    var samplesFilter = parseResult.GetValue(samplesOption);

    if (!configFile.Exists)
    {
        AnsiConsole.MarkupLine($"[red]Config file not found:[/] {Markup.Escape(configFile.FullName)}");
        return;
    }

    if (!datasetsDir.Exists)
    {
        AnsiConsole.MarkupLine($"[red]Datasets directory not found:[/] {Markup.Escape(datasetsDir.FullName)}");
        return;
    }

    // Load configuration
    var configJson = await File.ReadAllTextAsync(configFile.FullName);
    var benchmarkConfig = JsonSerializer.Deserialize<BenchmarkConfig>(configJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Failed to deserialize benchmark configuration.");

    AnsiConsole.MarkupLine($"[bold blue]Parlotype Benchmark[/] — {Markup.Escape(benchmarkConfig.Name)}");
    AnsiConsole.WriteLine();

    // Set up DI
    var services = new ServiceCollection();
    services.AddPlatformServices();
    services.AddSingleton<IModelDownloadService, HeadlessModelDownloadService>();
    services.AddLogging(builder =>
    {
        builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning);
        builder.AddZLoggerConsole();
    });

    await using var serviceProvider = services.BuildServiceProvider();

    var recognizer = serviceProvider.GetRequiredService<ISpeechRecognizer>();
    var vadService = benchmarkConfig.Vad.Enabled
        ? serviceProvider.GetRequiredService<IVadService>()
        : null;
    var logger = serviceProvider.GetRequiredService<ILogger<BenchmarkRunner>>();

    var runner = new BenchmarkRunner(recognizer, vadService, logger);

    // Run benchmark with progress
    BenchmarkResult? result = null;
    await AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .StartAsync("Running benchmark...", async ctx =>
        {
            var progress = new Progress<string>(msg => ctx.Status(msg));
            result = await runner.RunAsync(benchmarkConfig, datasetsDir.FullName, progress, tagsFilter, samplesFilter);
        });

    if (result is null)
        throw new InvalidOperationException("Benchmark run produced no result.");

    // Display results
    ConsoleReporter.DisplayResult(result);

    // Save results
    var savedPath = await JsonResultStore.SaveAsync(result, outputDir.FullName);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]Results saved to:[/] {Markup.Escape(savedPath)}");

    // Auto-index into SQLite
    var dbPath = Path.Combine(outputDir.FullName, "benchmarks.db");
    using (var index = new SqliteResultIndex(dbPath))
    {
        index.Index(result, savedPath);
    }

    AnsiConsole.MarkupLine($"[green]Indexed into:[/] {Markup.Escape(dbPath)}");
});

// --- import command ---
var importOutputOption = new Option<DirectoryInfo>("--output")
{
    Description = "Directory containing result JSON files and benchmarks.db",
    DefaultValueFactory = _ => new DirectoryInfo("./results"),
};

var importCommand = new Command("import", "Rebuild SQLite index from existing JSON result files")
{
    importOutputOption,
};

importCommand.SetAction(async parseResult =>
{
    var outputDir = parseResult.GetValue(importOutputOption)!;

    if (!outputDir.Exists)
    {
        AnsiConsole.MarkupLine($"[red]Directory not found:[/] {Markup.Escape(outputDir.FullName)}");
        return;
    }

    var jsonFiles = outputDir.GetFiles("*.json");
    if (jsonFiles.Length == 0)
    {
        AnsiConsole.MarkupLine("[yellow]No JSON result files found.[/]");
        return;
    }

    var dbPath = Path.Combine(outputDir.FullName, "benchmarks.db");
    using var index = new SqliteResultIndex(dbPath);
    var imported = 0;

    foreach (var file in jsonFiles)
    {
        try
        {
            var result = await JsonResultStore.LoadAsync(file.FullName);
            index.Index(result, file.FullName);
            imported++;
            AnsiConsole.MarkupLine($"  [green]✓[/] {Markup.Escape(file.Name)}");
        }
        catch (Exception ex)
        {
            AnsiConsole.MarkupLine($"  [red]✗[/] {Markup.Escape(file.Name)}: {Markup.Escape(ex.Message)}");
        }
    }

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]Imported {imported}/{jsonFiles.Length} files into {Markup.Escape(dbPath)}[/]");
});

// --- list command ---
var listOutputOption = new Option<DirectoryInfo>("--output")
{
    Description = "Directory containing benchmarks.db",
    DefaultValueFactory = _ => new DirectoryInfo("./results"),
};

var listModelOption = new Option<string?>("--model")
{
    Description = "Filter by model name",
};

var listConfigOption = new Option<string?>("--config")
{
    Description = "Filter by config name",
};

var listLimitOption = new Option<int?>("--last")
{
    Description = "Show only the last N runs",
};

var listCommand = new Command("list", "List historical benchmark runs")
{
    listOutputOption,
    listModelOption,
    listConfigOption,
    listLimitOption,
};

listCommand.SetAction(parseResult =>
{
    var outputDir = parseResult.GetValue(listOutputOption)!;
    var modelFilter = parseResult.GetValue(listModelOption);
    var configFilter = parseResult.GetValue(listConfigOption);
    var limit = parseResult.GetValue(listLimitOption);

    var dbPath = Path.Combine(outputDir.FullName, "benchmarks.db");
    if (!File.Exists(dbPath))
    {
        AnsiConsole.MarkupLine($"[yellow]No benchmark database found at {Markup.Escape(dbPath)}. Run a benchmark or use 'import' first.[/]");
        return;
    }

    using var index = new SqliteResultIndex(dbPath);
    var runs = index.ListRuns(modelFilter, configFilter, limit);
    ConsoleReporter.DisplayRunList(runs);
});

// --- compare command ---
var compareRunAOption = new Option<string>("--run-a")
{
    Description = "Run ID of the baseline run (A)",
    Required = true,
};

var compareRunBOption = new Option<string>("--run-b")
{
    Description = "Run ID of the comparison run (B)",
    Required = true,
};

var compareOutputDirOption = new Option<DirectoryInfo>("--output")
{
    Description = "Directory containing result files and benchmarks.db",
    DefaultValueFactory = _ => new DirectoryInfo("./results"),
};

var compareFormatOption = new Option<string>("--format")
{
    Description = "Output format: console, csv, markdown, json",
    DefaultValueFactory = _ => "console",
};

var compareCommand = new Command("compare", "Compare two benchmark runs side by side")
{
    compareRunAOption,
    compareRunBOption,
    compareOutputDirOption,
    compareFormatOption,
};

compareCommand.SetAction(async parseResult =>
{
    var runIdA = parseResult.GetValue(compareRunAOption)!;
    var runIdB = parseResult.GetValue(compareRunBOption)!;
    var outputDir = parseResult.GetValue(compareOutputDirOption)!;
    var format = parseResult.GetValue(compareFormatOption)!;

    var dbPath = Path.Combine(outputDir.FullName, "benchmarks.db");
    string? jsonPathA = null;
    string? jsonPathB = null;

    // Try to resolve JSON paths from SQLite index
    if (File.Exists(dbPath))
    {
        using var index = new SqliteResultIndex(dbPath);
        jsonPathA = index.GetJsonPath(runIdA);
        jsonPathB = index.GetJsonPath(runIdB);
    }

    // Fallback: search for JSON files matching the run IDs
    jsonPathA ??= FindJsonByRunId(outputDir.FullName, runIdA);
    jsonPathB ??= FindJsonByRunId(outputDir.FullName, runIdB);

    if (jsonPathA is null)
    {
        AnsiConsole.MarkupLine($"[red]Could not find result file for run A:[/] {Markup.Escape(runIdA)}");
        return;
    }

    if (jsonPathB is null)
    {
        AnsiConsole.MarkupLine($"[red]Could not find result file for run B:[/] {Markup.Escape(runIdB)}");
        return;
    }

    var resultA = await JsonResultStore.LoadAsync(jsonPathA);
    var resultB = await JsonResultStore.LoadAsync(jsonPathB);
    var comparison = ResultComparer.Compare(resultA, resultB);

    switch (format.ToLowerInvariant())
    {
        case "csv":
            Console.Write(CsvFormatter.FormatComparison(comparison));
            break;
        case "markdown" or "md":
            Console.Write(MarkdownFormatter.FormatComparison(comparison));
            break;
        case "json":
            var json = JsonSerializer.Serialize(comparison, new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
            Console.Write(json);
            break;
        default:
            ConsoleReporter.DisplayComparison(comparison);
            break;
    }
});

// --- export command ---
var exportRunIdOption = new Option<string>("--run-id")
{
    Description = "Run ID to export",
    Required = true,
};

var exportOutputDirOption = new Option<DirectoryInfo>("--output")
{
    Description = "Directory containing result files and benchmarks.db",
    DefaultValueFactory = _ => new DirectoryInfo("./results"),
};

var exportFormatOption = new Option<string>("--format")
{
    Description = "Export format: csv, markdown, json",
    Required = true,
};

var exportFileOption = new Option<FileInfo?>("--file")
{
    Description = "Output file path (defaults to stdout)",
};

var exportCommand = new Command("export", "Export a benchmark run in a specific format")
{
    exportRunIdOption,
    exportOutputDirOption,
    exportFormatOption,
    exportFileOption,
};

exportCommand.SetAction(async parseResult =>
{
    var runId = parseResult.GetValue(exportRunIdOption)!;
    var outputDir = parseResult.GetValue(exportOutputDirOption)!;
    var format = parseResult.GetValue(exportFormatOption)!;
    var exportFile = parseResult.GetValue(exportFileOption);

    var dbPath = Path.Combine(outputDir.FullName, "benchmarks.db");
    string? jsonPath = null;

    if (File.Exists(dbPath))
    {
        using var index = new SqliteResultIndex(dbPath);
        jsonPath = index.GetJsonPath(runId);
    }

    jsonPath ??= FindJsonByRunId(outputDir.FullName, runId);

    if (jsonPath is null)
    {
        AnsiConsole.MarkupLine($"[red]Could not find result file for run:[/] {Markup.Escape(runId)}");
        return;
    }

    var result = await JsonResultStore.LoadAsync(jsonPath);

    var output = format.ToLowerInvariant() switch
    {
        "csv" => CsvFormatter.FormatResult(result),
        "markdown" or "md" => MarkdownFormatter.FormatResult(result),
        "json" => JsonSerializer.Serialize(result, new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
        }),
        _ => throw new InvalidOperationException($"Unknown format: {format}. Use csv, markdown, or json."),
    };

    if (exportFile is not null)
    {
        await File.WriteAllTextAsync(exportFile.FullName, output);
        AnsiConsole.MarkupLine($"[green]Exported to:[/] {Markup.Escape(exportFile.FullName)}");
    }
    else
    {
        Console.Write(output);
    }
});

rootCommand.Add(runCommand);
rootCommand.Add(importCommand);
rootCommand.Add(listCommand);
rootCommand.Add(compareCommand);
rootCommand.Add(exportCommand);

var configuration = new CommandLineConfiguration(rootCommand);
return await configuration.InvokeAsync(args);

/// <summary>Finds a JSON result file that contains the given run ID in its filename.</summary>
static string? FindJsonByRunId(string directory, string runId)
{
    if (!Directory.Exists(directory))
        return null;

    // Exact filename match first (runId might be the timestamp-config pattern)
    var exactMatch = Directory.GetFiles(directory, $"{runId}.json").FirstOrDefault();
    if (exactMatch is not null)
        return exactMatch;

    // Partial match — run ID is embedded in filename
    return Directory.GetFiles(directory, "*.json")
        .FirstOrDefault(f => Path.GetFileNameWithoutExtension(f).Contains(runId, StringComparison.OrdinalIgnoreCase));
}
