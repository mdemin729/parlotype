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
/// <param name="HuggingFaceRevision">
/// The commit of <paramref name="HuggingFaceRepo"/> both files are downloaded
/// from (full SHA, never a branch). The ggml-org repos rename and re-convert
/// their files on <c>main</c>, so the commit, the file names and the digests
/// are one set: change them together (ADR-076).
/// </param>
/// <param name="GgufSha256">
/// SHA-256 of the GGUF file (lowercase hex), verified after download
/// (security audit 2026-07-11, S2). Sourced from HuggingFace LFS metadata at
/// <paramref name="HuggingFaceRevision"/>; null downloads unverified (with a
/// warning).
/// </param>
/// <param name="MmprojSha256">SHA-256 of the mmproj file; see <paramref name="GgufSha256"/>.</param>
public sealed record Gemma4ModelInfo(
    Gemma4Variant Variant,
    Gemma4Quant Quant,
    string ModelId,
    string DisplayName,
    string GgufFileName,
    string MmprojFileName,
    string DiskSize,
    string HuggingFaceRepo,
    string HuggingFaceRevision,
    string? GgufSha256 = null,
    string? MmprojSha256 = null)
{
    private const string E2BRepo = "ggml-org/gemma-4-E2B-it-GGUF";
    private const string E4BRepo = "ggml-org/gemma-4-E4B-it-GGUF";

    // The last upstream commits that carry every file below under these names
    // and digests (both 2026-04-12). On main the bf16 files were renamed to
    // BF16, Q4_K_M was deleted and Q8_0 was re-converted (2026-07-16).
    private const string E2BRevision = "a1dac71d3ab220618f5a7573a52acdc4baf3ae3b";
    private const string E4BRevision = "2714b5519c6c3516b1000e7c5e1eba998dfe1fe8";

    // mmproj (vision/audio projector) — bf16 projector paired with every quant
    // (small, highest quality, matches the known-good E4B configuration).
    private const string E2BMmproj = "mmproj-gemma-4-E2B-it-bf16.gguf";
    private const string E4BMmproj = "mmproj-gemma-4-E4B-it-bf16.gguf";
    private const string E2BMmprojSha256 = "e42083b71a9e31e0f722171d551f6d92b101544001c4dde040306a8f2160fe8c";
    private const string E4BMmprojSha256 = "4c199e460410ba219a8c63930a7121154e1c70cdf66044858f767966332e5a54";

    // --- E2B (the ggml-org E2B repo publishes no Q4_K_M) ---

    public static Gemma4ModelInfo E2B_Q8_0 { get; } = new(
        Variant: Gemma4Variant.E2B,
        Quant: Gemma4Quant.Q8_0,
        ModelId: "gemma-4-E2B-it-Q8_0",
        DisplayName: "Gemma 4 E2B (Q8_0)",
        GgufFileName: "gemma-4-E2B-it-Q8_0.gguf",
        MmprojFileName: E2BMmproj,
        DiskSize: "~5.5 GiB",
        HuggingFaceRepo: E2BRepo,
        HuggingFaceRevision: E2BRevision,
        GgufSha256: "e049411c01fb7a81161768c52e38828970e55a64e22738957adcbe51d20f1c8e",
        MmprojSha256: E2BMmprojSha256);

    public static Gemma4ModelInfo E2B_BF16 { get; } = new(
        Variant: Gemma4Variant.E2B,
        Quant: Gemma4Quant.BF16,
        ModelId: "gemma-4-E2B-it-bf16",
        DisplayName: "Gemma 4 E2B (BF16)",
        GgufFileName: "gemma-4-E2B-it-bf16.gguf",
        MmprojFileName: E2BMmproj,
        DiskSize: "~9.6 GiB",
        HuggingFaceRepo: E2BRepo,
        HuggingFaceRevision: E2BRevision,
        GgufSha256: "422dccfc1049b8691e36f2cd6e036e3dd33e9b8ae17b2521016abab776ffa630",
        MmprojSha256: E2BMmprojSha256);

    // --- E4B ---

    public static Gemma4ModelInfo E4B_Q4_K_M { get; } = new(
        Variant: Gemma4Variant.E4B,
        Quant: Gemma4Quant.Q4_K_M,
        ModelId: "gemma-4-E4B-it-Q4_K_M",
        DisplayName: "Gemma 4 E4B (Q4_K_M)",
        GgufFileName: "gemma-4-E4B-it-Q4_K_M.gguf",
        MmprojFileName: E4BMmproj,
        DiskSize: "~5.9 GiB",
        HuggingFaceRepo: E4BRepo,
        HuggingFaceRevision: E4BRevision,
        GgufSha256: "90ce98129eb3e8cc57e62433d500c97c624b1e3af1fcc85dd3b55ad7e0313e9f",
        MmprojSha256: E4BMmprojSha256);

    public static Gemma4ModelInfo E4B_Q8_0 { get; } = new(
        Variant: Gemma4Variant.E4B,
        Quant: Gemma4Quant.Q8_0,
        ModelId: "gemma-4-E4B-it-Q8_0",
        DisplayName: "Gemma 4 E4B (Q8_0)",
        GgufFileName: "gemma-4-E4B-it-Q8_0.gguf",
        MmprojFileName: E4BMmproj,
        DiskSize: "~8.4 GiB",
        HuggingFaceRepo: E4BRepo,
        HuggingFaceRevision: E4BRevision,
        GgufSha256: "fb8f0c032de00b18c710824af3c7e5777c71e5fb60b13f13575f0a9e92ddecd0",
        MmprojSha256: E4BMmprojSha256);

    public static Gemma4ModelInfo E4B_BF16 { get; } = new(
        Variant: Gemma4Variant.E4B,
        Quant: Gemma4Quant.BF16,
        ModelId: "gemma-4-E4B-it-bf16",
        DisplayName: "Gemma 4 E4B (BF16)",
        GgufFileName: "gemma-4-E4B-it-bf16.gguf",
        MmprojFileName: E4BMmproj,
        DiskSize: "~15 GiB",
        HuggingFaceRepo: E4BRepo,
        HuggingFaceRevision: E4BRevision,
        GgufSha256: "23458339ab520c5632ab8251ba42ed8f30baa29e0c74210b3fcbe9a4b047720c",
        MmprojSha256: E4BMmprojSha256);

    /// <summary>Expected SHA-256 for one of this model's files, or null when unknown.</summary>
    public string? GetSha256(string fileName) =>
        fileName == GgufFileName ? GgufSha256
        : fileName == MmprojFileName ? MmprojSha256
        : null;

    /// <summary>
    /// Download URL of one of this model's files, at <see cref="HuggingFaceRevision"/>.
    /// </summary>
    public string GetDownloadUrl(string fileName) =>
        HuggingFaceUrl.Resolve(HuggingFaceRepo, HuggingFaceRevision, fileName);

    /// <summary>The default model when no selection is persisted.</summary>
    public static Gemma4ModelInfo Default => E4B_Q4_K_M;

    /// <summary>All catalog entries in display order (E2B then E4B, ascending size).</summary>
    public static IReadOnlyList<Gemma4ModelInfo> All { get; } =
        [E2B_Q8_0, E2B_BF16, E4B_Q4_K_M, E4B_Q8_0, E4B_BF16];

    /// <summary>Resolves a catalog entry by its <see cref="ModelId"/>, or null.</summary>
    public static Gemma4ModelInfo? GetById(string? modelId) =>
        All.FirstOrDefault(m => m.ModelId == modelId);

    /// <summary>Returns the expected local directory for model files.</summary>
    public static string GetModelCacheDirectory() => Settings.AppPaths.Default.ModelsDirectory;
}
