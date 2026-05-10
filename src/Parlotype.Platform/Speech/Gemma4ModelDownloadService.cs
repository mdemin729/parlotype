using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>Downloads Gemma 4 GGUF and mmproj model files from HuggingFace.</summary>
public sealed class Gemma4ModelDownloadService
{
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly ILogger<Gemma4ModelDownloadService> _logger;

    public Gemma4ModelDownloadService(HttpClient httpClient, ILogger<Gemma4ModelDownloadService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Returns the local file path for the GGUF model.</summary>
    public static string GetGgufPath() =>
        Path.Combine(Gemma4ModelInfo.GetModelCacheDirectory(), Gemma4ModelInfo.Default.GgufFileName);

    /// <summary>Returns the local file path for the mmproj file.</summary>
    public static string GetMmprojPath() =>
        Path.Combine(Gemma4ModelInfo.GetModelCacheDirectory(), Gemma4ModelInfo.Default.MmprojFileName);

    /// <summary>Returns true if both GGUF and mmproj files are cached locally.</summary>
    public bool IsModelCached() =>
        File.Exists(GetGgufPath()) && File.Exists(GetMmprojPath());

    /// <summary>Returns true if the GGUF file is cached.</summary>
    public bool IsGgufCached() => File.Exists(GetGgufPath());

    /// <summary>Returns true if the mmproj file is cached.</summary>
    public bool IsMmprojCached() => File.Exists(GetMmprojPath());

    /// <summary>
    /// Downloads both model files if not already cached.
    /// Reports progress for the combined download.
    /// </summary>
    public async Task DownloadModelAsync(
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var model = Gemma4ModelInfo.Default;
        Directory.CreateDirectory(Gemma4ModelInfo.GetModelCacheDirectory());

        await DownloadLock.WaitAsync(cancellationToken);
        try
        {
            if (!IsGgufCached())
            {
                _logger.LogInformation("Downloading Gemma 4 GGUF: {FileName}", model.GgufFileName);
                await DownloadFileAsync(model.HuggingFaceRepo, model.GgufFileName,
                    GetGgufPath(), progress, "GGUF", cancellationToken);
            }

            if (!IsMmprojCached())
            {
                _logger.LogInformation("Downloading Gemma 4 mmproj: {FileName}", model.MmprojFileName);
                await DownloadFileAsync(model.HuggingFaceRepo, model.MmprojFileName,
                    GetMmprojPath(), progress, "mmproj", cancellationToken);
            }
        }
        finally
        {
            DownloadLock.Release();
        }
    }

    private async Task DownloadFileAsync(
        string repo,
        string fileName,
        string targetPath,
        IProgress<ModelDownloadProgress>? progress,
        string label,
        CancellationToken cancellationToken)
    {
        var url = $"https://huggingface.co/{repo}/resolve/main/{fileName}";
        var tempPath = targetPath + ".tmp";

        try
        {
            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength ?? 0;
            await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

            var buffer = new byte[81920];
            long downloadedBytes = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
            {
                await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                downloadedBytes += bytesRead;

                progress?.Report(new ModelDownloadProgress(
                    BytesReceived: downloadedBytes,
                    TotalBytes: totalBytes > 0 ? totalBytes : null));
            }

            // Atomic move
            File.Move(tempPath, targetPath, overwrite: true);
            _logger.LogInformation("Downloaded {Label}: {Path} ({Bytes:N0} bytes)", label, targetPath, downloadedBytes);
        }
        catch
        {
            // Clean up partial download
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); }
                catch { /* best effort */ }
            }
            throw;
        }
    }
}
