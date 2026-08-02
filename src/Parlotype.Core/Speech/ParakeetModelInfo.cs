namespace Parlotype.Core.Speech;

/// <summary>
/// Static metadata for a downloadable Parakeet ONNX model (encoder + decoder +
/// joiner + tokens, run via sherpa-onnx). Files keep their generic upstream
/// names (<c>encoder.int8.onnx</c> …), so each model gets its own subdirectory
/// under the shared model cache.
/// </summary>
/// <param name="EncoderWeightsFileName">
/// Optional ONNX external-data file for the encoder (fp32 exports keep weights
/// outside the graph). Must live next to the encoder file — onnxruntime
/// resolves it by relative path; null when the encoder is self-contained.
/// </param>
/// <param name="FileSha256">
/// Per-file SHA-256 digests (lowercase hex), keyed by file name, verified
/// after download (security audit 2026-07-11, S2). Sourced from the
/// HuggingFace LFS metadata of <paramref name="HuggingFaceRepo"/> at
/// <c>main</c>; <c>tokens.txt</c> is a non-LFS blob hashed directly.
/// A file missing from the map downloads unverified (with a warning).
/// </param>
public sealed record ParakeetModelInfo(
    string ModelId,
    string DisplayName,
    string HuggingFaceRepo,
    string EncoderFileName,
    string DecoderFileName,
    string JoinerFileName,
    string TokensFileName,
    string DiskSize,
    string? EncoderWeightsFileName = null,
    IReadOnlyDictionary<string, string>? FileSha256 = null)
{
    /// <summary>SHA-256 of the shared tokens.txt (identical blob in both repos).</summary>
    private const string TokensSha256 = "d58544679ea4bc6ac563d1f545eb7d474bd6cfa467f0a6e2c1dc1c7d37e3c35d";

    /// <summary>Parakeet TDT 0.6B v3, INT8 — 25 European languages, ~670 MB. Default.</summary>
    public static ParakeetModelInfo TdtV3Int8 { get; } = new(
        ModelId: "parakeet-tdt-0.6b-v3-int8",
        DisplayName: "Parakeet TDT 0.6B v3 (INT8)",
        HuggingFaceRepo: "csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8",
        EncoderFileName: "encoder.int8.onnx",
        DecoderFileName: "decoder.int8.onnx",
        JoinerFileName: "joiner.int8.onnx",
        TokensFileName: "tokens.txt",
        DiskSize: "~670 MB",
        FileSha256: new Dictionary<string, string>
        {
            ["encoder.int8.onnx"] = "acfc2b4456377e15d04f0243af540b7fe7c992f8d898d751cf134c3a55fd2247",
            ["decoder.int8.onnx"] = "179e50c43d1a9de79c8a24149a2f9bac6eb5981823f2a2ed88d655b24248db4e",
            ["joiner.int8.onnx"] = "3164c13fc2821009440d20fcb5fdc78bff28b4db2f8d0f0b329101719c0948b3",
            ["tokens.txt"] = TokensSha256,
        });

    /// <summary>
    /// Parakeet TDT 0.6B v3, full precision (fp32) — same 25 languages, ~2.6 GB.
    /// Slightly higher accuracy than INT8 at 4× the size and slower CPU decode.
    /// The encoder ships as a small graph plus an external weights file.
    /// </summary>
    public static ParakeetModelInfo TdtV3Fp32 { get; } = new(
        ModelId: "parakeet-tdt-0.6b-v3-fp32",
        DisplayName: "Parakeet TDT 0.6B v3 (Full precision)",
        HuggingFaceRepo: "csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3",
        EncoderFileName: "encoder.onnx",
        DecoderFileName: "decoder.onnx",
        JoinerFileName: "joiner.onnx",
        TokensFileName: "tokens.txt",
        DiskSize: "~2.6 GB",
        EncoderWeightsFileName: "encoder.weights",
        FileSha256: new Dictionary<string, string>
        {
            ["encoder.onnx"] = "3eed7ce424bf8339ad09233533c687e2dbd07e74ccf5027b5e7344019ea373b0",
            ["encoder.weights"] = "3af3f51af5f2d01dbbf5af47d42c7962a2c205f11004254bb4f2b979862f39a8",
            ["decoder.onnx"] = "d593cdb0e571f5a457ec2219af9968cbf6b0e8198e8f7839b40a8754593bf68c",
            ["joiner.onnx"] = "b9b0bcf88ac571902e69a6536223ed2d94885e981b85045410f1403d53121a63",
            ["tokens.txt"] = TokensSha256,
        });

    /// <summary>Expected SHA-256 for one of this model's files, or null when unknown.</summary>
    public string? GetSha256(string fileName) =>
        FileSha256 is not null && FileSha256.TryGetValue(fileName, out var sha) ? sha : null;

    /// <summary>The default model when no selection is persisted.</summary>
    public static ParakeetModelInfo Default => TdtV3Int8;

    /// <summary>All catalog entries in display order (default first).</summary>
    public static IReadOnlyList<ParakeetModelInfo> All { get; } = [TdtV3Int8, TdtV3Fp32];

    /// <summary>Resolves a catalog entry by its <see cref="ModelId"/>, or null.</summary>
    public static ParakeetModelInfo? GetById(string? modelId) =>
        All.FirstOrDefault(m => m.ModelId == modelId);

    /// <summary>All model file names, in download order (largest first).</summary>
    public IReadOnlyList<string> FileNames =>
        EncoderWeightsFileName is null
            ? [EncoderFileName, DecoderFileName, JoinerFileName, TokensFileName]
            : [EncoderWeightsFileName, EncoderFileName, DecoderFileName, JoinerFileName, TokensFileName];

    /// <summary>
    /// Returns this model's local cache directory
    /// (<c>%LOCALAPPDATA%\parlotype-data\models\&lt;ModelId&gt;</c> on Windows).
    /// </summary>
    public string GetModelDirectory() =>
        Path.Combine(Settings.AppPaths.Default.ModelsDirectory, ModelId);
}
