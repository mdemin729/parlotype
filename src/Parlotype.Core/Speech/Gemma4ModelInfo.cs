namespace Parlotype.Core.Speech;

/// <summary>Static metadata for a Gemma 4 GGUF model variant.</summary>
public sealed record Gemma4ModelInfo(
    string ModelId,
    string DisplayName,
    string GgufFileName,
    string MmprojFileName,
    string DiskSize,
    string HuggingFaceRepo)
{
    /// <summary>Default Gemma 4 E4B model (Q4_K_M quantization).</summary>
    public static Gemma4ModelInfo Default { get; } = new(
        ModelId: "gemma-4-E4B-it-Q4_K_M",
        DisplayName: "Gemma 4 E4B (Q4)",
        GgufFileName: "gemma-4-E4B-it-Q4_K_M.gguf",
        MmprojFileName: "mmproj-gemma-4-E4B-it-bf16.gguf",
        DiskSize: "~6 GiB",
        HuggingFaceRepo: "ggml-org/gemma-4-E4B-it-GGUF");

    /// <summary>Returns the expected local directory for model files.</summary>
    public static string GetModelCacheDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "models");
}
