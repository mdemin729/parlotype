namespace Parlotype.Core.Speech;

/// <summary>
/// Static metadata for a downloadable Parakeet ONNX model (encoder + decoder +
/// joiner + tokens, run via sherpa-onnx). Files keep their generic upstream
/// names (<c>encoder.int8.onnx</c> …), so each model gets its own subdirectory
/// under the shared model cache.
/// </summary>
public sealed record ParakeetModelInfo(
    string ModelId,
    string DisplayName,
    string HuggingFaceRepo,
    string EncoderFileName,
    string DecoderFileName,
    string JoinerFileName,
    string TokensFileName,
    string DiskSize)
{
    /// <summary>Parakeet TDT 0.6B v3, INT8 — 25 European languages, ~670 MB.</summary>
    public static ParakeetModelInfo TdtV3Int8 { get; } = new(
        ModelId: "parakeet-tdt-0.6b-v3-int8",
        DisplayName: "Parakeet TDT 0.6B v3 (INT8)",
        HuggingFaceRepo: "csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8",
        EncoderFileName: "encoder.int8.onnx",
        DecoderFileName: "decoder.int8.onnx",
        JoinerFileName: "joiner.int8.onnx",
        TokensFileName: "tokens.txt",
        DiskSize: "~670 MB");

    /// <summary>The default model when no selection is persisted.</summary>
    public static ParakeetModelInfo Default => TdtV3Int8;

    /// <summary>All catalog entries in display order.</summary>
    public static IReadOnlyList<ParakeetModelInfo> All { get; } = [TdtV3Int8];

    /// <summary>Resolves a catalog entry by its <see cref="ModelId"/>, or null.</summary>
    public static ParakeetModelInfo? GetById(string? modelId) =>
        All.FirstOrDefault(m => m.ModelId == modelId);

    /// <summary>All model file names, in download order (largest first).</summary>
    public IReadOnlyList<string> FileNames =>
        [EncoderFileName, DecoderFileName, JoinerFileName, TokensFileName];

    /// <summary>
    /// Returns this model's local cache directory
    /// (<c>%LOCALAPPDATA%\parlotype\models\&lt;ModelId&gt;</c>).
    /// </summary>
    public string GetModelDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "models", ModelId);
}
