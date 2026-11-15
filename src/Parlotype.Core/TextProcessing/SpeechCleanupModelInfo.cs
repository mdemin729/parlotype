namespace Parlotype.Core.TextProcessing;

/// <summary>
/// Static metadata for a downloadable speech cleanup model: a GGUF text model
/// that <c>llama-server</c> hosts and that rewrites raw recognizer output as
/// clean written text (ADR-073).
/// </summary>
/// <param name="DisplayName">
/// The name shown to the user. For S1-mini the licence requires this exact
/// wording and capitalization wherever the model is used, so it is never
/// translated or restyled.
/// </param>
/// <param name="HuggingFaceRevision">
/// The commit of <paramref name="HuggingFaceRepo"/> the file is downloaded
/// from (full SHA, never a branch), so a later upstream release cannot replace
/// the bytes behind <paramref name="GgufSha256"/> (ADR-076).
/// </param>
/// <param name="GgufSha256">
/// SHA-256 of the GGUF file (lowercase hex), verified after download. Sourced
/// from the HuggingFace LFS metadata of <paramref name="HuggingFaceRepo"/> at
/// <paramref name="HuggingFaceRevision"/>.
/// </param>
/// <param name="ContextTokens">
/// Context window the server is started with. Far below what the model
/// supports: a dictation is a few hundred tokens, and a small context keeps
/// the load fast and the memory low.
/// </param>
public sealed record SpeechCleanupModelInfo(
    string ModelId,
    string DisplayName,
    string GgufFileName,
    string DiskSize,
    string HuggingFaceRepo,
    string HuggingFaceRevision,
    string GgufSha256,
    int ContextTokens)
{
    /// <summary>
    /// S1-mini by Superwhisper, release v1, Q4_K_M: a 0.6B fine-tune of
    /// Qwen3-0.6B that does one job, normalizing speech-to-text output. English
    /// only. Apache 2.0 plus a clause that the name is kept.
    /// </summary>
    public static SpeechCleanupModelInfo S1Mini { get; } = new(
        ModelId: "s1-mini-q4_k_m",
        DisplayName: "S1-mini by Superwhisper",
        GgufFileName: "s1-mini-q4_k_m.gguf",
        DiskSize: "462 MB",
        HuggingFaceRepo: "superwhisper/s1-mini-GGUF",
        HuggingFaceRevision: "34add00a48a2e5d24e5a4ee5405a99620a3a240c",
        GgufSha256: "3b41ebe2502cbd03e811d5d16b022f5ab551eda58d62597d152f89535003c634",
        ContextTokens: 4096);

    /// <summary>Download URL of the GGUF file, at <see cref="HuggingFaceRevision"/>.</summary>
    public string DownloadUrl =>
        Speech.HuggingFaceUrl.Resolve(HuggingFaceRepo, HuggingFaceRevision, GgufFileName);

    /// <summary>The model used when nothing else is selected.</summary>
    public static SpeechCleanupModelInfo Default => S1Mini;

    /// <summary>All catalog entries in display order.</summary>
    public static IReadOnlyList<SpeechCleanupModelInfo> All { get; } = [S1Mini];
}
