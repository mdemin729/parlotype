namespace Parlotype.Core.Speech.LlamaServer;

/// <summary>
/// A llama.cpp server install resolved on disk. Managed installs originate
/// from <see cref="ILlamaServerInstaller"/>; manual installs are user-pointed
/// folders that Parlotype does not own.
/// </summary>
public sealed record LlamaServerInstall(
    string Id,
    LlamaServerSource Source,
    string? Build,
    LlamaServerBackend? Backend,
    string AbsolutePath,
    DateTimeOffset InstalledAt,
    bool IsValid);
