namespace Parlotype.Core.Speech;

/// <summary>Static metadata for a Whisper GGML model.</summary>
public sealed record WhisperModelInfo(
    WhisperModelType Type,
    string DisplayName,
    string DiskSize,
    string Sha,
    bool SupportsTranslation)
{
    // SupportsTranslation is false for English-only models (trained on English audio
    // only) and for Large v3 Turbo (a distilled, transcription-only model).
    private static readonly WhisperModelInfo[] All =
    [
        new(WhisperModelType.Tiny,          "Tiny",             "75 MiB",   "bd577a113a864445d4c299885e0cb97d4ba92b5f", SupportsTranslation: true),
        new(WhisperModelType.TinyEn,        "Tiny (English)",   "75 MiB",   "c78c86eb1a8faa21b369bcd33207cc90d64ae9df", SupportsTranslation: false),
        new(WhisperModelType.Base,          "Base",             "142 MiB",  "465707469ff3a37a2b9b8d8f89f2f99de7299dac", SupportsTranslation: true),
        new(WhisperModelType.BaseEn,        "Base (English)",   "142 MiB",  "137c40403d78fd54d454da0f9bd998f78703390c", SupportsTranslation: false),
        new(WhisperModelType.Small,         "Small",            "466 MiB",  "55356645c2b361a969dfd0ef2c5a50d530afd8d5", SupportsTranslation: true),
        new(WhisperModelType.SmallEn,       "Small (English)",  "466 MiB",  "db8a495a91d927739e50b3fc1cc4c6b8f6c2d022", SupportsTranslation: false),
        new(WhisperModelType.Medium,        "Medium",           "1.5 GiB",  "fd9727b6e1217c2f614f9b698455c4ffd82463b4", SupportsTranslation: true),
        new(WhisperModelType.MediumEn,      "Medium (English)", "1.5 GiB",  "8c30f0e44ce9560643ebd10bbe50cd20eafd3723", SupportsTranslation: false),
        new(WhisperModelType.LargeV1,       "Large v1",         "2.9 GiB",  "b1caaf735c4cc1429223d5a74f0f4d0b9b59a299", SupportsTranslation: true),
        new(WhisperModelType.LargeV2,       "Large v2",         "2.9 GiB",  "0f4c8e34f21cf1a914c59d8b3ce882345ad349d6", SupportsTranslation: true),
        new(WhisperModelType.LargeV3,       "Large v3",         "2.9 GiB",  "ad82bf6a9043ceed055076d0fd39f5f186ff8062", SupportsTranslation: true),
        new(WhisperModelType.LargeV3Turbo,  "Large v3 Turbo",   "1.5 GiB",  "4af2b29d7ec73d781377bfd1758ca957a807e941", SupportsTranslation: false),
    ];

    private static readonly Dictionary<WhisperModelType, WhisperModelInfo> ByType =
        All.ToDictionary(m => m.Type);

    /// <summary>Returns metadata for all available models.</summary>
    public static IReadOnlyList<WhisperModelInfo> GetAll() => All;

    /// <summary>Returns metadata for a specific model type.</summary>
    public static WhisperModelInfo Get(WhisperModelType type) => ByType[type];
}
