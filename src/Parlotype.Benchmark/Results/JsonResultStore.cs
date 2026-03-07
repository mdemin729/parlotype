using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parlotype.Benchmark.Results;

/// <summary>Saves and loads benchmark results as JSON files.</summary>
public static class JsonResultStore
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter() },
    };

    /// <summary>Saves a benchmark result to a JSON file in the specified output directory.</summary>
    /// <returns>The full path of the saved file.</returns>
    public static async Task<string> SaveAsync(BenchmarkResult result, string outputDir, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDir);

        var timestamp = result.Timestamp.ToString("yyyyMMdd-HHmmss");
        var safeName = string.Join("-", result.Configuration.Name.Split(Path.GetInvalidFileNameChars()));
        var fileName = $"{timestamp}-{safeName}.json";
        var filePath = Path.Combine(outputDir, fileName);

        var json = JsonSerializer.Serialize(result, SerializerOptions);
        await File.WriteAllTextAsync(filePath, json, cancellationToken);

        return filePath;
    }

    /// <summary>Loads a benchmark result from a JSON file.</summary>
    public static async Task<BenchmarkResult> LoadAsync(string filePath, CancellationToken cancellationToken = default)
    {
        var json = await File.ReadAllTextAsync(filePath, cancellationToken);
        return JsonSerializer.Deserialize<BenchmarkResult>(json, SerializerOptions)
               ?? throw new InvalidOperationException($"Failed to deserialize benchmark result from {filePath}");
    }
}
