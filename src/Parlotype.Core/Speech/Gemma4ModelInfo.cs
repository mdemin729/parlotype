namespace Parlotype.Core.Speech;

/// <summary>The Gemma 4 model size family.</summary>
public enum Gemma4Variant
{
    /// <summary>Smaller, faster (fewer active params).</summary>
    E2B,

    /// <summary>Larger, more accurate.</summary>
    E4B,
}

/// <summary>Quantization level of a Gemma 4 GGUF.</summary>
public enum Gemma4Quant
{
    /// <summary>4-bit (smallest, E4B only in the ggml-org repos).</summary>
    Q4_K_M,

    /// <summary>8-bit.</summary>
    Q8_0,

    /// <summary>16-bit bfloat16 (largest; may hallucinate on some GPUs).</summary>
    BF16,
}

/// <summary>Static metadata for a downloadable Gemma 4 GGUF model variant.</summary>
public sealed record Gemma4ModelInfo(
    Gemma4Variant Variant,
    Gemma4Quant Quant,
    string ModelId,
    string DisplayName,
    string GgufFileName,
    string MmprojFileName,
    string DiskSize,
    string HuggingFaceRepo)
{
    private const string E2BRepo = "ggml-org/gemma-4-E2B-it-GGUF";
    private const string E4BRepo = "ggml-org/gemma-4-E4B-it-GGUF";

    // mmproj (vision/audio projector) — bf16 projector paired with every quant
    // (small, highest quality, matches the known-good E4B configuration).
    private const string E2BMmproj = "mmproj-gemma-4-E2B-it-bf16.gguf";
    private const string E4BMmproj = "mmproj-gemma-4-E4B-it-bf16.gguf";

    // --- E2B (the ggml-org E2B repo publishes no Q4_K_M) ---

    public static Gemma4ModelInfo E2B_Q8_0 { get; } = new(
        Variant: Gemma4Variant.E2B,
        Quant: Gemma4Quant.Q8_0,
        ModelId: "gemma-4-E2B-it-Q8_0",
        DisplayName: "Gemma 4 E2B (Q8_0)",
        GgufFileName: "gemma-4-E2B-it-Q8_0.gguf",
        MmprojFileName: E2BMmproj,
        DiskSize: "~5.5 GiB",
        HuggingFaceRepo: E2BRepo);

    public static Gemma4ModelInfo E2B_BF16 { get; } = new(
        Variant: Gemma4Variant.E2B,
        Quant: Gemma4Quant.BF16,
        ModelId: "gemma-4-E2B-it-bf16",
        DisplayName: "Gemma 4 E2B (BF16)",
        GgufFileName: "gemma-4-E2B-it-bf16.gguf",
        MmprojFileName: E2BMmproj,
        DiskSize: "~9.6 GiB",
        HuggingFaceRepo: E2BRepo);

    // --- E4B ---

    public static Gemma4ModelInfo E4B_Q4_K_M { get; } = new(
        Variant: Gemma4Variant.E4B,
        Quant: Gemma4Quant.Q4_K_M,
        ModelId: "gemma-4-E4B-it-Q4_K_M",
        DisplayName: "Gemma 4 E4B (Q4_K_M)",
        GgufFileName: "gemma-4-E4B-it-Q4_K_M.gguf",
        MmprojFileName: E4BMmproj,
        DiskSize: "~5.9 GiB",
        HuggingFaceRepo: E4BRepo);

    public static Gemma4ModelInfo E4B_Q8_0 { get; } = new(
        Variant: Gemma4Variant.E4B,
        Quant: Gemma4Quant.Q8_0,
        ModelId: "gemma-4-E4B-it-Q8_0",
        DisplayName: "Gemma 4 E4B (Q8_0)",
        GgufFileName: "gemma-4-E4B-it-Q8_0.gguf",
        MmprojFileName: E4BMmproj,
        DiskSize: "~8.4 GiB",
        HuggingFaceRepo: E4BRepo);

    public static Gemma4ModelInfo E4B_BF16 { get; } = new(
        Variant: Gemma4Variant.E4B,
        Quant: Gemma4Quant.BF16,
        ModelId: "gemma-4-E4B-it-bf16",
        DisplayName: "Gemma 4 E4B (BF16)",
        GgufFileName: "gemma-4-E4B-it-bf16.gguf",
        MmprojFileName: E4BMmproj,
        DiskSize: "~15 GiB",
        HuggingFaceRepo: E4BRepo);

    /// <summary>The default model when no selection is persisted.</summary>
    public static Gemma4ModelInfo Default => E4B_Q4_K_M;

    /// <summary>All catalog entries in display order (E2B then E4B, ascending size).</summary>
    public static IReadOnlyList<Gemma4ModelInfo> All { get; } =
        [E2B_Q8_0, E2B_BF16, E4B_Q4_K_M, E4B_Q8_0, E4B_BF16];

    /// <summary>Resolves a catalog entry by its <see cref="ModelId"/>, or null.</summary>
    public static Gemma4ModelInfo? GetById(string? modelId) =>
        All.FirstOrDefault(m => m.ModelId == modelId);

    /// <summary>Returns the expected local directory for model files.</summary>
    public static string GetModelCacheDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "models");
}
