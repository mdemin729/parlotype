using System.Runtime.InteropServices;
using System.Text.Json.Serialization;

namespace Parlotype.Benchmark.Results;

/// <summary>Captures runtime environment information for reproducibility.</summary>
public sealed class EnvironmentInfo
{
    [JsonPropertyName("os")]
    public string Os { get; init; } = RuntimeInformation.OSDescription;

    [JsonPropertyName("architecture")]
    public string Architecture { get; init; } = RuntimeInformation.ProcessArchitecture.ToString();

    [JsonPropertyName("dotnetVersion")]
    public string DotnetVersion { get; init; } = Environment.Version.ToString();

    [JsonPropertyName("processorCount")]
    public int ProcessorCount { get; init; } = Environment.ProcessorCount;

    public static EnvironmentInfo Capture() => new();
}
