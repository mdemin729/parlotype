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
    public WhisperConfig? Whisper { get; init; }

    [JsonPropertyName("llamaCpp")]
    public LlamaCppConfig? LlamaCpp { get; init; }

    [JsonPropertyName("vad")]
    public VadConfig Vad { get; init; } = new();

    /// <summary>Returns true when the Gemma 4 llama.cpp backend is selected.</summary>
    [JsonIgnore]
    public bool IsLlamaCpp => LlamaCpp is not null;

    /// <summary>Returns the effective Whisper config, defaulting if neither engine is specified.</summary>
    [JsonIgnore]
    public WhisperConfig EffectiveWhisper => Whisper ?? new WhisperConfig();

    /// <summary>Returns a display-friendly engine name.</summary>
    [JsonIgnore]
    public string EngineName => IsLlamaCpp ? "Gemma4" : "Whisper";

    /// <summary>Returns a display-friendly model identifier.</summary>
    [JsonIgnore]
    public string ModelDisplayName => IsLlamaCpp
        ? LlamaCpp!.ModelId
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

/// <summary>Configuration for the Gemma 4 llama.cpp engine in benchmark runs.</summary>
public sealed class LlamaCppConfig
{
    /// <summary>Gemma 4 GGUF catalog model ID (e.g. "gemma-4-E4B-it-Q4_K_M").
    /// Must match a <see cref="Parlotype.Core.Speech.Gemma4ModelInfo"/> catalog entry.</summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; init; } = "gemma-4-E4B-it-Q4_K_M";

    /// <summary>Port for the llama-server process (default 8321).</summary>
    [JsonPropertyName("port")]
    public int Port { get; init; } = 8321;

    /// <summary>Optional path override to the folder containing llama-server.exe.
    /// When null, falls back to the llama-server registry or
    /// <c>%LOCALAPPDATA%/parlotype/llama-server</c>.</summary>
    [JsonPropertyName("serverFolder")]
    public string? ServerFolder { get; init; }
}
