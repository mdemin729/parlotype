namespace Parlotype.Core.LlamaServer;

/// <summary>Downloads, verifies, extracts, and removes managed llama.cpp server installs.</summary>
public interface ILlamaServerInstaller
{
    /// <summary>
    /// Downloads the variant (and its companion when present), verifies SHA256
    /// when available, extracts atomically, and registers the install.
    /// Throws on cancel; leaves no partial state on disk.
    /// </summary>
    Task<LlamaServerInstall> InstallAsync(
        LlamaServerVariant variant,
        IProgress<LlamaServerInstallProgress>? progress,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Removes a managed install. If the install is currently active, the
    /// sidecar is stopped first (Windows file-lock release).
    /// </summary>
    Task UninstallAsync(string installId, CancellationToken cancellationToken = default);
}
