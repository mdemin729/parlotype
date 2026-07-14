namespace Parlotype.Core.Speech;

/// <summary>Static metadata for a Whisper GGML model.</summary>
/// <param name="Sha256">
/// SHA-256 digest of the GGML file, verified after download (security audit
/// 2026-07-11, S2). Values sourced from the HuggingFace LFS metadata of the
/// exact repo/revision the downloader uses
/// (<c>sandrohanea/whisper.net</c>, revision <c>v3</c>, <c>classic/</c>).
/// </param>
public sealed record WhisperModelInfo(
    WhisperModelType Type,
    string DisplayName,
    string DiskSize,
    string Sha256,
    bool SupportsTranslation)
{
    // SupportsTranslation is false for English-only models (trained on English audio
    // only) and for Large v3 Turbo (a distilled, transcription-only model).
    private static readonly WhisperModelInfo[] All =
    [
        new(WhisperModelType.Tiny,          "Tiny",             "75 MiB",   "be07e048e1e599ad46341c8d2a135645097a538221678b7acdd1b1919c6e1b21", SupportsTranslation: true),
        new(WhisperModelType.TinyEn,        "Tiny (English)",   "75 MiB",   "921e4cf8686fdd993dcd081a5da5b6c365bfde1162e72b08d75ac75289920b1f", SupportsTranslation: false),
        new(WhisperModelType.Base,          "Base",             "142 MiB",  "60ed5bc3dd14eea856493d334349b405782ddcaf0028d4b5df4088345fba2efe", SupportsTranslation: true),
        new(WhisperModelType.BaseEn,        "Base (English)",   "142 MiB",  "a03779c86df3323075f5e796cb2ce5029f00ec8869eee3fdfb897afe36c6d002", SupportsTranslation: false),
        new(WhisperModelType.Small,         "Small",            "466 MiB",  "1be3a9b2063867b937e64e2ec7483364a79917e157fa98c5d94b5c1fffea987b", SupportsTranslation: true),
        new(WhisperModelType.SmallEn,       "Small (English)",  "466 MiB",  "c6138d6d58ecc8322097e0f987c32f1be8bb0a18532a3f88f734d1bbf9c41e5d", SupportsTranslation: false),
        new(WhisperModelType.Medium,        "Medium",           "1.5 GiB",  "6c14d5adee5f86394037b4e4e8b59f1673b6cee10e3cf0b11bbdbee79c156208", SupportsTranslation: true),
        new(WhisperModelType.MediumEn,      "Medium (English)", "1.5 GiB",  "cc37e93478338ec7700281a7ac30a10128929eb8f427dda2e865faa8f6da4356", SupportsTranslation: false),
        new(WhisperModelType.LargeV1,       "Large v1",         "2.9 GiB",  "7d99f41a10525d0206bddadd86760181fa920438b6b33237e3118ff6c83bb53d", SupportsTranslation: true),
        new(WhisperModelType.LargeV2,       "Large v2",         "2.9 GiB",  "9a423fe4d40c82774b6af34115b8b935f34152246eb19e80e376071d3f999487", SupportsTranslation: true),
        new(WhisperModelType.LargeV3,       "Large v3",         "2.9 GiB",  "64d182b440b98d5203c4f9bd541544d84c605196c4f7b845dfa11fb23594d1e2", SupportsTranslation: true),
        new(WhisperModelType.LargeV3Turbo,  "Large v3 Turbo",   "1.5 GiB",  "1fc70f774d38eb169993ac391eea357ef47c88757ef72ee5943879b7e8e2bc69", SupportsTranslation: false),
    ];

    private static readonly Dictionary<WhisperModelType, WhisperModelInfo> ByType =
        All.ToDictionary(m => m.Type);

    /// <summary>Returns metadata for all available models.</summary>
    public static IReadOnlyList<WhisperModelInfo> GetAll() => All;

    /// <summary>Returns metadata for a specific model type.</summary>
    public static WhisperModelInfo Get(WhisperModelType type) => ByType[type];
}
