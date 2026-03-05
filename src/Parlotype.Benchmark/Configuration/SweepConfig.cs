using System.Text.Json;
using System.Text.Json.Serialization;

namespace Parlotype.Benchmark.Configuration;

/// <summary>Configuration for a parameter sweep benchmark that generates multiple run configurations.</summary>
public sealed class SweepConfig
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "unnamed-sweep";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("datasets")]
    public string[] Datasets { get; init; } = [];

    [JsonPropertyName("repetitions")]
    public int Repetitions { get; init; } = 1;

    [JsonPropertyName("sweep")]
    public Dictionary<string, JsonElement[]> Sweep { get; init; } = new();

    [JsonPropertyName("vad")]
    public VadConfig Vad { get; init; } = new();
}
