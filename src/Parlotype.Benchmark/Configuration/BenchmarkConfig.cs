using System.Text.Json.Serialization;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Configuration;

/// <summary>Benchmark run configuration, deserialized from JSON.</summary>
public sealed class BenchmarkConfig
{
    [JsonPropertyName("name")]
    public string Name { get; init; } = "unnamed";

    [JsonPropertyName("description")]
    public string? Description { get; init; }

    [JsonPropertyName("datasets")]
    public string[] Datasets { get; init; } = [];

    [JsonPropertyName("repetitions")]
    public int Repetitions { get; init; } = 1;

    [JsonPropertyName("whisper")]
    public WhisperConfig Whisper { get; init; } = new();

    [JsonPropertyName("vad")]
    public VadConfig Vad { get; init; } = new();
}

public sealed class WhisperConfig
{
    [JsonPropertyName("model")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WhisperModelType Model { get; init; } = WhisperModelType.Base;

    [JsonPropertyName("language")]
    public string Language { get; init; } = "auto";

    [JsonPropertyName("beamSize")]
    public int BeamSize { get; init; } = 1;

    [JsonPropertyName("temperature")]
    public float Temperature { get; init; } = 0.0f;

    [JsonPropertyName("initialPrompt")]
    public string? InitialPrompt { get; init; }

    [JsonPropertyName("threads")]
    public int? Threads { get; init; }
}

public sealed class VadConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;
}
