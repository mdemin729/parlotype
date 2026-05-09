using System.Text.Json.Serialization;

namespace Parlotype.Gemma4;

/// <summary>Configuration for the Gemma 4 speech recognition sidecar.</summary>
public sealed class Gemma4Config
{
    /// <summary>HuggingFace model identifier (e.g. "gemma-4-E2B-it", "gemma-4-E4B-it").</summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; init; } = "gemma-4-E2B-it";

    /// <summary>
    /// Local path to the pre-downloaded model. When null, defaults to
    /// <c>{LocalAppData}/parlotype/models/{ModelId}</c>.
    /// </summary>
    [JsonPropertyName("modelPath")]
    public string? ModelPath { get; init; }

    /// <summary>Quantization mode: "none" (BF16, default), "4bit", or "8bit". Note: 4-bit/8-bit
    /// quantization via bitsandbytes may fail on Gemma 4's audio encoder layers.</summary>
    [JsonPropertyName("quantization")]
    public string Quantization { get; init; } = "none";

    /// <summary>Localhost port for the Python sidecar server.</summary>
    [JsonPropertyName("port")]
    public int Port { get; init; } = 8321;

    /// <summary>Path to the Python executable.</summary>
    [JsonPropertyName("pythonPath")]
    public string PythonPath { get; init; } = "python";

    /// <summary>Maximum tokens the model will generate per transcription.</summary>
    [JsonPropertyName("maxNewTokens")]
    public int MaxNewTokens { get; init; } = 200;

    /// <summary>Torch dtype for model weights: "bfloat16" (default), "float32", or "float16".</summary>
    [JsonPropertyName("dtype")]
    public string Dtype { get; init; } = "bfloat16";

    /// <summary>Torch device map (e.g. "auto", "cpu", "cuda:0").</summary>
    [JsonPropertyName("deviceMap")]
    public string DeviceMap { get; init; } = "auto";

    /// <summary>Timeout in seconds to wait for the sidecar to become ready.</summary>
    [JsonPropertyName("startupTimeoutSeconds")]
    public int StartupTimeoutSeconds { get; init; } = 180;

    /// <summary>Resolves the effective model path, expanding environment variables.</summary>
    public string GetEffectiveModelPath()
    {
        if (!string.IsNullOrEmpty(ModelPath))
            return Environment.ExpandEnvironmentVariables(ModelPath);

        var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        return Path.Combine(localAppData, "parlotype", "models", ModelId);
    }
}
