using System.Text.Json.Serialization;
using Parlotype.Core.Speech;
using Parlotype.Gemma4;

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
    public WhisperConfig? Whisper { get; init; }

    [JsonPropertyName("gemma4")]
    public Gemma4Config? Gemma4 { get; init; }

    [JsonPropertyName("vad")]
    public VadConfig Vad { get; init; } = new();

    /// <summary>Returns true when the Gemma 4 backend is selected.</summary>
    [JsonIgnore]
    public bool IsGemma4 => Gemma4 is not null;

    /// <summary>Returns the effective Whisper config, defaulting if neither engine is specified.</summary>
    [JsonIgnore]
    public WhisperConfig EffectiveWhisper => Whisper ?? new WhisperConfig();

    /// <summary>Returns a display-friendly engine name.</summary>
    [JsonIgnore]
    public string EngineName => IsGemma4 ? "Gemma4" : "Whisper";

    /// <summary>Returns a display-friendly model identifier.</summary>
    [JsonIgnore]
    public string ModelDisplayName => IsGemma4
        ? $"Gemma4/{Gemma4!.ModelId} ({Gemma4.Quantization})"
        : EffectiveWhisper.Model.ToString();
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

    [JsonPropertyName("runtimePreference")]
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public RuntimePreference RuntimePreference { get; init; } = RuntimePreference.Auto;
}

public sealed record VadConfig
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; init; } = true;

    [JsonPropertyName("threshold")]
    public float Threshold { get; init; } = 0.5f;

    [JsonPropertyName("speechPadMs")]
    public int SpeechPadMs { get; init; } = 400;

    [JsonPropertyName("minSilenceDurationMs")]
    public int MinSilenceDurationMs { get; init; } = 500;

    [JsonPropertyName("minSpeechDurationMs")]
    public int MinSpeechDurationMs { get; init; } = 50;

    [JsonPropertyName("interSegmentSilenceMs")]
    public int InterSegmentSilenceMs { get; init; } = 160;

    /// <summary>Pipeline flush silence threshold in milliseconds. When set, the benchmark
    /// simulates real-time AudioPipelineService behavior: incremental VAD in 500ms chunks
    /// with silence-triggered flushing. Each flush is transcribed separately and results
    /// are concatenated. When null (default), the entire file is processed in one shot.</summary>
    [JsonPropertyName("silenceThresholdMs")]
    public int? SilenceThresholdMs { get; init; }
}
