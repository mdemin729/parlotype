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

var runCommand = new Command("run", "Execute a benchmark run")
{
    configOption,
    datasetsOption,
    outputOption,
    verboseOption,
};

runCommand.SetAction(async parseResult =>
{
    var configFile = parseResult.GetValue(configOption)!;
    var datasetsDir = parseResult.GetValue(datasetsOption)!;
    var outputDir = parseResult.GetValue(outputOption)!;
    var verbose = parseResult.GetValue(verboseOption);

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
            result = await runner.RunAsync(benchmarkConfig, datasetsDir.FullName, progress);
        });

    if (result is null)
        throw new InvalidOperationException("Benchmark run produced no result.");

    // Display results
    ConsoleReporter.DisplayResult(result);

    // Save results
    var savedPath = await JsonResultStore.SaveAsync(result, outputDir.FullName);
    AnsiConsole.WriteLine();
    AnsiConsole.MarkupLine($"[green]Results saved to:[/] {Markup.Escape(savedPath)}");
});

rootCommand.Add(runCommand);

var configuration = new CommandLineConfiguration(rootCommand);
return await configuration.InvokeAsync(args);
