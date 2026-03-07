using System.Text.Json.Serialization;

namespace Parlotype.Benchmark.Configuration;

/// <summary>Dataset manifest describing test samples and their ground truth.</summary>
public sealed class DatasetManifest
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("language")]
    public string Language { get; init; } = "en";

    [JsonPropertyName("samples")]
    public List<SampleInfo> Samples { get; init; } = [];
}

public sealed class SampleInfo
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("file")]
    public string File { get; init; } = "";

    [JsonPropertyName("referenceText")]
    public string ReferenceText { get; init; } = "";

    [JsonPropertyName("language")]
    public string? Language { get; init; }

    [JsonPropertyName("durationSeconds")]
    public double? DurationSeconds { get; init; }

    [JsonPropertyName("tags")]
    public string[] Tags { get; init; } = [];
}
