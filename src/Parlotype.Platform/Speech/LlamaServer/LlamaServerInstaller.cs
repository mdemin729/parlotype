using System.IO.Compression;
using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech.LlamaServer;

namespace Parlotype.Platform.Speech.LlamaServer;

/// <summary>
/// Installs managed llama.cpp server builds: downloads main + companion
/// archives to a staging directory, verifies SHA256 when provided, extracts
/// into a merged payload folder, then atomically renames into the final
/// install folder and records the entry in the manifest. Any failure path
/// removes the staging directory so no partial state is visible.
/// </summary>
public sealed class LlamaServerInstaller : ILlamaServerInstaller
{
    internal const string StagingDirName = ".staging";
    internal const string PayloadSubDir = "payload";
    internal const string MainArchiveBaseName = "main";
    internal const string CompanionArchiveName = "companion.zip";

    private readonly string _rootDirectory;
    private readonly StreamingFileDownloader _downloader;
    private readonly ILlamaServerRegistry _registry;
    private readonly ILlamaCppServerLifecycle? _lifecycle;
    private readonly ILogger<LlamaServerInstaller> _logger;
    private readonly Func<DateTimeOffset> _now;

    public LlamaServerInstaller(
        StreamingFileDownloader downloader,
        ILlamaServerRegistry registry,
        ILlamaCppServerLifecycle? lifecycle,
        ILogger<LlamaServerInstaller> logger)
        : this(
            JsonLlamaServerRegistry.DefaultRootDirectory(),
            downloader,
            registry,
            lifecycle,
            logger,
            () => DateTimeOffset.UtcNow)
    { }

    internal LlamaServerInstaller(
        string rootDirectory,
        StreamingFileDownloader downloader,
        ILlamaServerRegistry registry,
        ILlamaCppServerLifecycle? lifecycle,
        ILogger<LlamaServerInstaller> logger,
        Func<DateTimeOffset> nowProvider)
    {
        _rootDirectory = rootDirectory;
        _downloader = downloader;
        _registry = registry;
        _lifecycle = lifecycle;
        _logger = logger;
        _now = nowProvider;
    }

    public async Task<LlamaServerInstall> InstallAsync(
        LlamaServerVariant variant,
        IProgress<LlamaServerInstallProgress>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(variant);

        var installId = BuildInstallId(variant);
        var finalPath = Path.Combine(_rootDirectory, installId);

        EnsureDiskSpace(variant.Bytes + (variant.CompanionBytes ?? 0));

        var stagingDir = Path.Combine(_rootDirectory, StagingDirName, Guid.NewGuid().ToString("N"));
        var payloadDir = Path.Combine(stagingDir, PayloadSubDir);
        Directory.CreateDirectory(payloadDir);

        try
        {
            var mainArchive = Path.Combine(stagingDir, MainArchiveBaseName + GetArchiveExtension(variant.Os));
            await DownloadAsync(
                variant.DownloadUrl, mainArchive, variant.Bytes,
                "downloading", progress, cancellationToken);
            await VerifySha256Async(mainArchive, variant.Sha256, variant.AssetName, cancellationToken);

            string? companionArchive = null;
            if (!string.IsNullOrWhiteSpace(variant.CompanionDownloadUrl))
            {
                companionArchive = Path.Combine(stagingDir, CompanionArchiveName);
                await DownloadAsync(
                    variant.CompanionDownloadUrl!, companionArchive, variant.CompanionBytes,
                    "downloading-companion", progress, cancellationToken);
                await VerifySha256Async(
                    companionArchive, variant.CompanionSha256,
                    variant.CompanionAssetName ?? "companion", cancellationToken);
            }

            progress?.Report(new LlamaServerInstallProgress("extracting", 0, null));
            ExtractArchive(mainArchive, payloadDir, variant.Os);
            if (companionArchive is not null)
                ExtractArchive(companionArchive, payloadDir, LlamaServerOs.Windows);

            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report(new LlamaServerInstallProgress("finalizing", 0, null));
            CommitInstallFolder(payloadDir, finalPath);

            var record = new LlamaServerManagedInstallRecord(
                Id: installId,
                Build: variant.Build,
                Backend: variant.Backend,
                Os: variant.Os,
                Arch: variant.Arch,
                AssetName: variant.AssetName,
                CompanionAssetName: variant.CompanionAssetName,
                Sha256: variant.Sha256,
                CompanionSha256: variant.CompanionSha256,
                InstalledAt: _now());
            await _registry.AddOrUpdateAsync(record, cancellationToken);

            _logger.LogInformation(
                "Installed llama-server {Id} ({Backend} {Os}/{Arch}) from {Asset}",
                installId, variant.Backend, variant.Os, variant.Arch, variant.AssetName);

            return (await _registry.GetManagedAsync(installId, cancellationToken))
                ?? throw new InvalidOperationException(
                    $"Install completed but registry lookup for {installId} returned null.");
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Install of {Asset} cancelled.", variant.AssetName);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install of {Asset} failed; cleaning up staging.", variant.AssetName);
            throw;
        }
        finally
        {
            TryDeleteDirectory(stagingDir);
        }
    }

    public async Task UninstallAsync(string installId, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(installId);

        var install = await _registry.GetManagedAsync(installId, cancellationToken);
        if (install is null)
        {
            _logger.LogInformation("Uninstall: {Id} is not in the manifest; nothing to do.", installId);
            return;
        }

        var active = await _registry.GetActiveAsync(cancellationToken);
        if (active is not null && active.Id == installId)
        {
            if (_lifecycle is not null)
            {
                _logger.LogInformation(
                    "Stopping active llama-server before uninstalling {Id}.", installId);
                await _lifecycle.StopForReplacementAsync(cancellationToken);
            }
            // Clear the active selector so the next start does not point at a stale id.
            await _registry.SetActiveAsync(installId: null, LlamaServerSource.Managed, cancellationToken);
        }

        if (Directory.Exists(install.AbsolutePath))
        {
            try
            {
                Directory.Delete(install.AbsolutePath, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "Failed to delete install folder {Path}; manifest entry left in place.",
                    install.AbsolutePath);
                throw;
            }
        }

        await _registry.RemoveAsync(installId, cancellationToken);
        _logger.LogInformation("Uninstalled llama-server {Id}.", installId);
    }

    /// <summary>
    /// Derives a deterministic, human-readable install id from the variant.
    /// e.g. <c>"llama-b9198-bin-win-cuda-12.4-x64.zip"</c>
    /// becomes <c>"b9198-win-cuda-12.4-x64"</c>. Stripping <c>llama-</c> and
    /// the <c>-bin-</c> infix keeps full CUDA minor versions (which the
    /// <see cref="LlamaServerBackend"/> enum collapses). Used by the UI to
    /// match catalog variants against installed entries.
    /// </summary>
    public static string BuildInstallId(LlamaServerVariant variant)
    {
        var name = variant.AssetName;
        if (name.StartsWith("llama-", StringComparison.Ordinal))
            name = name["llama-".Length..];
        name = name.Replace("-bin-", "-", StringComparison.Ordinal);

        var tarGzIdx = name.LastIndexOf(".tar.gz", StringComparison.OrdinalIgnoreCase);
        if (tarGzIdx >= 0)
            name = name[..tarGzIdx];
        else
        {
            var dotIdx = name.LastIndexOf('.');
            if (dotIdx >= 0)
                name = name[..dotIdx];
        }
        return name;
    }

    private async Task DownloadAsync(
        string url,
        string destination,
        long? expectedBytes,
        string phase,
        IProgress<LlamaServerInstallProgress>? progress,
        CancellationToken cancellationToken)
    {
        IProgress<StreamingDownloadProgress>? bridged = progress is null
            ? null
            : new Progress<StreamingDownloadProgress>(p =>
                progress.Report(new LlamaServerInstallProgress(
                    phase, p.BytesReceived, p.TotalBytes ?? expectedBytes)));

        await _downloader.DownloadAsync(url, destination, bridged, cancellationToken);
    }

    private async Task VerifySha256Async(
        string filePath,
        string? expectedHex,
        string label,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(expectedHex))
        {
            _logger.LogWarning(
                "No SHA256 provided for {Label}; integrity check skipped.", label);
            return;
        }

        using var sha = SHA256.Create();
        await using var stream = File.OpenRead(filePath);
        var hash = await sha.ComputeHashAsync(stream, cancellationToken);
        var actualHex = Convert.ToHexString(hash);

        if (!string.Equals(actualHex, expectedHex, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"SHA256 mismatch for {label}: expected {expectedHex.ToLowerInvariant()}, " +
                $"got {actualHex.ToLowerInvariant()}.");
        }

        _logger.LogDebug("SHA256 verified for {Label}.", label);
    }

    private static string GetArchiveExtension(LlamaServerOs os) => os switch
    {
        LlamaServerOs.Windows => ".zip",
        LlamaServerOs.MacOs => ".tar.gz",
        LlamaServerOs.Linux => ".tar.gz",
        _ => throw new NotSupportedException($"Unsupported OS: {os}"),
    };

    private static void ExtractArchive(string archivePath, string destination, LlamaServerOs os)
    {
        switch (os)
        {
            case LlamaServerOs.Windows:
                ZipFile.ExtractToDirectory(archivePath, destination, overwriteFiles: true);
                break;
            case LlamaServerOs.MacOs:
            case LlamaServerOs.Linux:
                throw new NotSupportedException(
                    $"tar.gz extraction for {os} is not yet implemented (phase-1 is Windows-only).");
            default:
                throw new NotSupportedException($"Unsupported OS for extraction: {os}.");
        }
    }

    private static void CommitInstallFolder(string payloadDir, string finalPath)
    {
        if (Directory.Exists(finalPath))
            Directory.Delete(finalPath, recursive: true);

        var parent = Path.GetDirectoryName(finalPath);
        if (!string.IsNullOrEmpty(parent))
            Directory.CreateDirectory(parent);

        Directory.Move(payloadDir, finalPath);
    }

    private void EnsureDiskSpace(long bytesNeeded)
    {
        if (bytesNeeded <= 0) return;
        var required = bytesNeeded * 3;

        try
        {
            var rootForDrive = Path.GetPathRoot(Path.GetFullPath(_rootDirectory));
            if (string.IsNullOrEmpty(rootForDrive)) return;
            var drive = new DriveInfo(rootForDrive);
            if (drive.AvailableFreeSpace < required)
            {
                throw new IOException(
                    $"Insufficient disk space on {drive.Name}: need ~{required / 1_000_000} MB " +
                    $"(asset {bytesNeeded / 1_000_000} MB + extraction headroom), " +
                    $"available {drive.AvailableFreeSpace / 1_000_000} MB.");
            }
        }
        catch (IOException)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Disk-space precheck failed; continuing without it.");
        }
    }

    private void TryDeleteDirectory(string path)
    {
        if (!Directory.Exists(path)) return;
        try
        {
            Directory.Delete(path, recursive: true);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not delete staging directory {Path}.", path);
        }
    }
}
