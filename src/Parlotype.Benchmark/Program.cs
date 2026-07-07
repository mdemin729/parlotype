using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Benchmark.Configuration;
using Parlotype.Benchmark.Pipeline;
using Parlotype.Benchmark.Reporting;
using Parlotype.Benchmark.Results;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform;
using Parlotype.Platform.Speech;
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

var gpuOption = new Option<bool>("--gpu")
{
    Description = "Enable GPU acceleration (CUDA). Use --gpu false to force CPU-only.",
    DefaultValueFactory = _ => true,
};

var runCommand = new Command("run", "Execute a benchmark run")
{
    configOption,
    datasetsOption,
    outputOption,
    verboseOption,
    tagsOption,
    samplesOption,
    gpuOption,
};

runCommand.SetAction(async parseResult =>
{
    var configFile = parseResult.GetValue(configOption)!;
    var datasetsDir = parseResult.GetValue(datasetsOption)!;
    var outputDir = parseResult.GetValue(outputOption)!;
    var verbose = parseResult.GetValue(verboseOption);
    var tagsFilter = parseResult.GetValue(tagsOption);
    var samplesFilter = parseResult.GetValue(samplesOption);
    var gpu = parseResult.GetValue(gpuOption);

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

    // CLI --gpu flag overrides config: --gpu false forces CPU-only
    if (!gpu)
    {
        if (benchmarkConfig.IsLlamaCpp)
        {
            // GPU control for llama-server is via -ngl; the benchmark cannot override it
            // without modifying the Platform layer.  Emit a warning and continue.
            AnsiConsole.MarkupLine("[yellow]Warning: --gpu false is not supported for the Gemma4 " +
                "(llama.cpp) engine. llama-server controls GPU usage internally. Ignoring.[/]");
        }
        else if (benchmarkConfig.IsParakeet)
        {
            // Parakeet (sherpa-onnx) is CPU-only — nothing to override.
        }
        else
        {
            var w = benchmarkConfig.EffectiveWhisper;
            benchmarkConfig = new BenchmarkConfig
            {
                Name = benchmarkConfig.Name,
                Description = benchmarkConfig.Description,
                Datasets = benchmarkConfig.Datasets,
                Repetitions = benchmarkConfig.Repetitions,
                Whisper = new WhisperConfig
                {
                    Model = w.Model,
                    Language = w.Language,
                    BeamSize = w.BeamSize,
                    Temperature = w.Temperature,
                    InitialPrompt = w.InitialPrompt,
                    Threads = w.Threads,
                    RuntimePreference = RuntimePreference.Cpu,
                },
                Vad = benchmarkConfig.Vad,
            };
        }
    }

    AnsiConsole.MarkupLine($"[bold blue]Parlotype Benchmark[/] — {Markup.Escape(benchmarkConfig.Name)}");
    AnsiConsole.MarkupLine($"[bold]Engine:[/] {benchmarkConfig.EngineName} — {benchmarkConfig.ModelDisplayName}");
    AnsiConsole.WriteLine();

    // Set up DI and create recognizer
    var services = new ServiceCollection();
    services.AddPlatformServices();
    services.AddLogging(builder =>
    {
        builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning);
        builder.AddZLoggerConsole();
    });

    if (benchmarkConfig.IsLlamaCpp)
    {
        // Override ISettingsService so LlamaCppSpeechRecognizer reads from the benchmark
        // config rather than the user's personal settings.json.
        var llamaCpp = benchmarkConfig.LlamaCpp!;
        services.AddSingleton<ISettingsService>(new InMemorySettingsService(
            new Dictionary<string, string?>
            {
                [SettingsKeys.LlamaCppPort]        = llamaCpp.Port.ToString(),
                [SettingsKeys.SelectedGemma4Model] = llamaCpp.ModelId,
                [SettingsKeys.LlamaCppServerFolder] = llamaCpp.ServerFolder,
            }));
        // Model must already be present on disk; no download service needed.
    }
    else if (benchmarkConfig.IsParakeet)
    {
        // Override ISettingsService so ParakeetSpeechRecognizer reads the model
        // from the benchmark config rather than the user's settings.json.
        services.AddSingleton<ISettingsService>(new InMemorySettingsService(
            new Dictionary<string, string?>
            {
                [SettingsKeys.SelectedParakeetModel] = benchmarkConfig.Parakeet!.ModelId,
            }));
    }
    else
    {
        services.AddSingleton<IModelDownloadService, HeadlessModelDownloadService>();
    }

    await using var serviceProvider = services.BuildServiceProvider();

    ISpeechRecognizer recognizer;
    if (benchmarkConfig.IsLlamaCpp)
    {
        // Resolve the concrete type directly so we bypass DelegatingSpeechRecognizer,
        // which would read SpeechEngine from settings and might route to Whisper.
        recognizer = serviceProvider.GetRequiredService<LlamaCppSpeechRecognizer>();
    }
    else if (benchmarkConfig.IsParakeet)
    {
        // Headless download when missing (files are small next to Gemma; the
        // Whisper path auto-downloads too, so Parakeet matches that UX).
        var parakeetModel = ParakeetModelInfo.GetById(benchmarkConfig.Parakeet!.ModelId)
            ?? throw new InvalidOperationException(
                $"Unknown Parakeet model id '{benchmarkConfig.Parakeet!.ModelId}'. " +
                $"Known ids: {string.Join(", ", ParakeetModelInfo.All.Select(m => m.ModelId))}");
        var parakeetDownloader = serviceProvider.GetRequiredService<ParakeetModelDownloadService>();
        if (!parakeetDownloader.IsModelCached(parakeetModel))
        {
            AnsiConsole.MarkupLine($"[yellow]Downloading Parakeet model {Markup.Escape(parakeetModel.DisplayName)} ({parakeetModel.DiskSize})...[/]");
            await parakeetDownloader.DownloadModelAsync(parakeetModel, progress: null, CancellationToken.None);
        }

        recognizer = serviceProvider.GetRequiredService<ParakeetSpeechRecognizer>();
    }
    else
    {
        recognizer = serviceProvider.GetRequiredService<ISpeechRecognizer>();
    }

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
    // Note: LlamaCppSpeechRecognizer is managed by the DI container (await using serviceProvider).
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

// --- check command ---
var checkBaselineOption = new Option<string>("--baseline")
{
    Description = "Run ID of the baseline to compare against, or 'latest' for most recent",
    Required = true,
};

var checkCurrentOption = new Option<string>("--current")
{
    Description = "Run ID of the current run to check, or 'latest' for most recent",
    Required = true,
};

var checkOutputDirOption = new Option<DirectoryInfo>("--output")
{
    Description = "Directory containing result files and benchmarks.db",
    DefaultValueFactory = _ => new DirectoryInfo("./results"),
};

var checkMaxWerOption = new Option<double>("--max-wer-delta")
{
    Description = "Maximum allowed WER increase (percentage points)",
    DefaultValueFactory = _ => 2.0,
};

var checkMaxCerOption = new Option<double>("--max-cer-delta")
{
    Description = "Maximum allowed CER increase (percentage points)",
    DefaultValueFactory = _ => 1.0,
};

var checkMaxRtfOption = new Option<double>("--max-rtf-delta")
{
    Description = "Maximum allowed RTF increase",
    DefaultValueFactory = _ => 0.1,
};

var checkCommand = new Command("check", "Check for regressions against a baseline run (exits with code 1 on failure)")
{
    checkBaselineOption,
    checkCurrentOption,
    checkOutputDirOption,
    checkMaxWerOption,
    checkMaxCerOption,
    checkMaxRtfOption,
};

checkCommand.SetAction(async parseResult =>
{
    var baselineId = parseResult.GetValue(checkBaselineOption)!;
    var currentId = parseResult.GetValue(checkCurrentOption)!;
    var outputDir = parseResult.GetValue(checkOutputDirOption)!;
    var maxWerDelta = parseResult.GetValue(checkMaxWerOption);
    var maxCerDelta = parseResult.GetValue(checkMaxCerOption);
    var maxRtfDelta = parseResult.GetValue(checkMaxRtfOption);

    var dbPath = Path.Combine(outputDir.FullName, "benchmarks.db");
    if (!File.Exists(dbPath))
    {
        AnsiConsole.MarkupLine($"[red]No benchmark database found at {Markup.Escape(dbPath)}.[/]");
        parseResult.Configuration.Output.Write("FAIL: No benchmark database found.\n");
        Environment.ExitCode = 1;
        return;
    }

    using var index = new SqliteResultIndex(dbPath);

    // Resolve 'latest' alias
    var resolvedBaseline = ResolveRunId(index, baselineId);
    var resolvedCurrent = ResolveRunId(index, currentId);

    if (resolvedBaseline is null)
    {
        AnsiConsole.MarkupLine($"[red]Baseline run not found:[/] {Markup.Escape(baselineId)}");
        Environment.ExitCode = 1;
        return;
    }

    if (resolvedCurrent is null)
    {
        AnsiConsole.MarkupLine($"[red]Current run not found:[/] {Markup.Escape(currentId)}");
        Environment.ExitCode = 1;
        return;
    }

    // Load results
    var baselineJsonPath = index.GetJsonPath(resolvedBaseline) ?? FindJsonByRunId(outputDir.FullName, resolvedBaseline);
    var currentJsonPath = index.GetJsonPath(resolvedCurrent) ?? FindJsonByRunId(outputDir.FullName, resolvedCurrent);

    if (baselineJsonPath is null || currentJsonPath is null)
    {
        AnsiConsole.MarkupLine("[red]Could not locate JSON result files for the specified runs.[/]");
        Environment.ExitCode = 1;
        return;
    }

    var baselineResult = await JsonResultStore.LoadAsync(baselineJsonPath);
    var currentResult = await JsonResultStore.LoadAsync(currentJsonPath);
    var comparison = ResultComparer.Compare(baselineResult, currentResult);

    // Check thresholds
    var failures = new List<string>();

    if (comparison.WerDelta.Absolute > maxWerDelta)
        failures.Add($"WER: +{comparison.WerDelta.Absolute:F2}% (max allowed: +{maxWerDelta:F2}%)");

    if (comparison.CerDelta.Absolute > maxCerDelta)
        failures.Add($"CER: +{comparison.CerDelta.Absolute:F2}% (max allowed: +{maxCerDelta:F2}%)");

    if (comparison.RtfDelta.Absolute > maxRtfDelta)
        failures.Add($"RTF: +{comparison.RtfDelta.Absolute:F4} (max allowed: +{maxRtfDelta:F4})");

    // Display results
    AnsiConsole.MarkupLine($"[bold]Regression Check[/]");
    AnsiConsole.MarkupLine($"  Baseline: {Markup.Escape(resolvedBaseline)}");
    AnsiConsole.MarkupLine($"  Current:  {Markup.Escape(resolvedCurrent)}");
    AnsiConsole.WriteLine();

    AnsiConsole.MarkupLine($"  WER: {comparison.WerDelta.ValueA:F1}% → {comparison.WerDelta.ValueB:F1}% (Δ {comparison.WerDelta.Absolute:+0.0;-0.0}%)");
    AnsiConsole.MarkupLine($"  CER: {comparison.CerDelta.ValueA:F1}% → {comparison.CerDelta.ValueB:F1}% (Δ {comparison.CerDelta.Absolute:+0.0;-0.0}%)");
    AnsiConsole.MarkupLine($"  RTF: {comparison.RtfDelta.ValueA:F3} → {comparison.RtfDelta.ValueB:F3} (Δ {comparison.RtfDelta.Absolute:+0.000;-0.000})");
    AnsiConsole.WriteLine();

    if (failures.Count == 0)
    {
        AnsiConsole.MarkupLine("[green bold]✅ PASS — No regressions detected.[/]");
        Environment.ExitCode = 0;
    }
    else
    {
        AnsiConsole.MarkupLine("[red bold]❌ FAIL — Regressions detected:[/]");
        foreach (var failure in failures)
        {
            AnsiConsole.MarkupLine($"  [red]• {Markup.Escape(failure)}[/]");
        }
        Environment.ExitCode = 1;
    }
});

// --- sweep command ---
var sweepConfigOption = new Option<FileInfo>("--config")
{
    Description = "Path to sweep configuration JSON file",
    Required = true,
};

var sweepDatasetsOption = new Option<DirectoryInfo>("--datasets")
{
    Description = "Root directory containing dataset folders",
    Required = true,
};

var sweepOutputOption = new Option<DirectoryInfo>("--output")
{
    Description = "Output directory for result files",
    DefaultValueFactory = _ => new DirectoryInfo("./results"),
};

var sweepVerboseOption = new Option<bool>("--verbose", "-v")
{
    Description = "Enable verbose logging",
};

var sweepGpuOption = new Option<bool>("--gpu")
{
    Description = "Enable GPU acceleration (CUDA). Use --gpu false to force CPU-only.",
    DefaultValueFactory = _ => true,
};

var sweepCommand = new Command("sweep", "Run a parameter sweep across multiple configurations")
{
    sweepConfigOption,
    sweepDatasetsOption,
    sweepOutputOption,
    sweepVerboseOption,
    sweepGpuOption,
};

sweepCommand.SetAction(async parseResult =>
{
    var configFile = parseResult.GetValue(sweepConfigOption)!;
    var datasetsDir = parseResult.GetValue(sweepDatasetsOption)!;
    var outputDir = parseResult.GetValue(sweepOutputOption)!;
    var verbose = parseResult.GetValue(sweepVerboseOption);
    var gpu = parseResult.GetValue(sweepGpuOption);

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

    // Load sweep configuration
    var configJson = await File.ReadAllTextAsync(configFile.FullName);
    var sweepConfig = JsonSerializer.Deserialize<SweepConfig>(configJson,
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true })
        ?? throw new InvalidOperationException("Failed to deserialize sweep configuration.");

    // Expand into individual configs
    var configs = SweepExpander.Expand(sweepConfig);

    // CLI --gpu flag overrides config: --gpu false forces CPU-only
    if (!gpu)
    {
        configs = configs.Select(cfg =>
        {
            var w = cfg.EffectiveWhisper;
            return new BenchmarkConfig
            {
                Name = cfg.Name,
                Description = cfg.Description,
                Datasets = cfg.Datasets,
                Repetitions = cfg.Repetitions,
                Whisper = new WhisperConfig
                {
                    Model = w.Model,
                    Language = w.Language,
                    BeamSize = w.BeamSize,
                    Temperature = w.Temperature,
                    InitialPrompt = w.InitialPrompt,
                    Threads = w.Threads,
                    RuntimePreference = RuntimePreference.Cpu,
                },
                Vad = cfg.Vad,
            };
        }).ToList();
    }

    AnsiConsole.MarkupLine($"[bold blue]Parlotype Benchmark — Sweep:[/] {Markup.Escape(sweepConfig.Name)}");
    AnsiConsole.MarkupLine($"  [grey]Configurations: {configs.Count}[/]");
    AnsiConsole.MarkupLine($"  [grey]Repetitions per config: {sweepConfig.Repetitions}[/]");
    AnsiConsole.WriteLine();

    var allResults = new List<BenchmarkResult>();
    var dbPath = Path.Combine(outputDir.FullName, "benchmarks.db");

    for (int c = 0; c < configs.Count; c++)
    {
        var benchmarkConfig = configs[c];
        AnsiConsole.MarkupLine($"[bold]Configuration {c + 1}/{configs.Count}:[/] {Markup.Escape(benchmarkConfig.Name)}");

        // Create a fresh DI container for each config (to reinitialize the Whisper model)
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

        BenchmarkResult? result = null;
        await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"Running {benchmarkConfig.Name}...", async ctx =>
            {
                var progress = new Progress<string>(msg => ctx.Status(msg));
                result = await runner.RunAsync(benchmarkConfig, datasetsDir.FullName, progress);
            });

        if (result is null)
            throw new InvalidOperationException($"Sweep config '{benchmarkConfig.Name}' produced no result.");

        allResults.Add(result);

        // Save and index
        var savedPath = await JsonResultStore.SaveAsync(result, outputDir.FullName);
        using (var index = new SqliteResultIndex(dbPath))
        {
            index.Index(result, savedPath);
        }

        AnsiConsole.MarkupLine($"  [green]✓[/] WER={result.Summary.AverageWer:F1}%, RTF={result.Summary.AverageRtf:F3}");
        AnsiConsole.WriteLine();
    }

    // Display sweep summary
    ConsoleReporter.DisplaySweepSummary(allResults);

    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]All {configs.Count} configurations complete. Results in {Markup.Escape(outputDir.FullName)}[/]");
});

rootCommand.Add(runCommand);
rootCommand.Add(importCommand);
rootCommand.Add(listCommand);
rootCommand.Add(compareCommand);
rootCommand.Add(exportCommand);
rootCommand.Add(checkCommand);
rootCommand.Add(sweepCommand);

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

/// <summary>Resolves a run ID, supporting 'latest' alias and partial matching.</summary>
static string? ResolveRunId(SqliteResultIndex index, string runId)
{
    if (runId.Equals("latest", StringComparison.OrdinalIgnoreCase))
    {
        var runs = index.ListRuns(limit: 1);
        return runs.Count > 0 ? runs[0].RunId : null;
    }

    if (index.Contains(runId))
        return runId;

    // Try partial match
    var allRuns = index.ListRuns();
    return allRuns.FirstOrDefault(r => r.RunId.Contains(runId, StringComparison.OrdinalIgnoreCase))?.RunId;
}
