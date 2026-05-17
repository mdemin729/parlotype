namespace Parlotype.Core.Speech.LlamaServer;

/// <summary>
/// One installable llama.cpp server asset from a GitHub release. CUDA Windows
/// builds carry an optional companion (the matching cudart zip) that must be
/// downloaded and extracted into the same install folder.
/// </summary>
public sealed record LlamaServerVariant(
    string Build,
    LlamaServerBackend Backend,
    LlamaServerOs Os,
    LlamaServerArch Arch,
    string AssetName,
    long Bytes,
    string DownloadUrl,
    string? Sha256,
    string? CompanionAssetName,
    string? CompanionDownloadUrl,
    long? CompanionBytes,
    string? CompanionSha256);
