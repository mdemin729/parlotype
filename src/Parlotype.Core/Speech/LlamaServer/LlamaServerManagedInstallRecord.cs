namespace Parlotype.Core.Speech.LlamaServer;

/// <summary>
/// Full manifest entry for a managed install, including audit metadata
/// (source asset name, SHA256 digests). This is the write model handed to
/// <see cref="ILlamaServerRegistry.AddOrUpdateAsync"/>; the read model
/// <see cref="LlamaServerInstall"/> intentionally omits these fields.
/// </summary>
public sealed record LlamaServerManagedInstallRecord(
    string Id,
    string Build,
    LlamaServerBackend Backend,
    LlamaServerOs Os,
    LlamaServerArch Arch,
    string AssetName,
    string? CompanionAssetName,
    string? Sha256,
    string? CompanionSha256,
    DateTimeOffset InstalledAt);
