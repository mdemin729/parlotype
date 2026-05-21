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

    /// <summary>Returns the local file path for the GGUF model of a variant.</summary>
    public static string GetGgufPath(Gemma4ModelInfo model) =>
        Path.Combine(Gemma4ModelInfo.GetModelCacheDirectory(), model.GgufFileName);

    /// <summary>Returns the local file path for the mmproj file of a variant.</summary>
    public static string GetMmprojPath(Gemma4ModelInfo model) =>
        Path.Combine(Gemma4ModelInfo.GetModelCacheDirectory(), model.MmprojFileName);

    /// <summary>Returns true if both GGUF and mmproj files for a variant are cached locally.</summary>
    public bool IsModelCached(Gemma4ModelInfo model) =>
        IsGgufCached(model) && IsMmprojCached(model);

    /// <summary>Returns true if the GGUF file for a variant is cached.</summary>
    public bool IsGgufCached(Gemma4ModelInfo model) => File.Exists(GetGgufPath(model));

    /// <summary>Returns true if the mmproj file for a variant is cached.</summary>
    public bool IsMmprojCached(Gemma4ModelInfo model) => File.Exists(GetMmprojPath(model));

    /// <summary>
    /// Downloads both model files for a variant if not already cached.
    /// Reports a single cumulative progress across the GGUF + mmproj pair so
    /// the UI shows one combined "X / total" figure rather than resetting
    /// per file.
    /// </summary>
    public async Task DownloadModelAsync(
        Gemma4ModelInfo model,
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Gemma4ModelInfo.GetModelCacheDirectory());

        await DownloadLock.WaitAsync(cancellationToken);
        try
        {
            var pending = new List<(string FileName, string TargetPath, string Label)>();
            if (!IsGgufCached(model))
                pending.Add((model.GgufFileName, GetGgufPath(model), "GGUF"));
            if (!IsMmprojCached(model))
                pending.Add((model.MmprojFileName, GetMmprojPath(model), "mmproj"));

            // Pre-compute the combined size via HEAD so progress is reported
            // against the whole download, not each file. If any size is
            // unknown we fall back to per-file totals (offset + current).
            var grandTotal = 0L;
            var totalKnown = true;
            foreach (var (fileName, _, _) in pending)
            {
                var size = await GetContentLengthAsync(BuildUrl(model.HuggingFaceRepo, fileName), cancellationToken);
                if (size is > 0)
                    grandTotal += size.Value;
                else
                    totalKnown = false;
            }

            var completedBytes = 0L;
            foreach (var (fileName, targetPath, label) in pending)
            {
                _logger.LogInformation("Downloading Gemma 4 {Label}: {FileName}", label, fileName);
                var fileBytes = await DownloadFileAsync(
                    model.HuggingFaceRepo, fileName, targetPath, progress, label,
                    cumulativeOffset: completedBytes,
                    grandTotal: totalKnown ? grandTotal : null,
                    cancellationToken);
                completedBytes += fileBytes;
            }
        }
        finally
        {
            DownloadLock.Release();
        }
    }

    private static string BuildUrl(string repo, string fileName) =>
        $"https://huggingface.co/{repo}/resolve/main/{fileName}";

    private async Task<long?> GetContentLengthAsync(string url, CancellationToken cancellationToken)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Head, url);
            using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            return response.IsSuccessStatusCode ? response.Content.Headers.ContentLength : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // Best effort — fall back to per-file progress if HEAD fails.
            _logger.LogDebug(ex, "HEAD request for size failed: {Url}", url);
            return null;
        }
    }

    /// <summary>Deletes the GGUF and mmproj files for a variant if present.</summary>
    public async Task DeleteModelAsync(Gemma4ModelInfo model)
    {
        await DownloadLock.WaitAsync();
        try
        {
            foreach (var path in new[] { GetGgufPath(model), GetMmprojPath(model) })
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.LogInformation("Deleted Gemma 4 model file: {Path}", path);
                }
            }
        }
        finally
        {
            DownloadLock.Release();
        }
    }

    /// <summary>
    /// Downloads a single file, reporting cumulative progress.
    /// <paramref name="cumulativeOffset"/> is the byte count of already-completed
    /// sibling files; <paramref name="grandTotal"/> is the combined size of the
    /// whole download (null when unknown, in which case progress is reported
    /// against this file's own size offset by <paramref name="cumulativeOffset"/>).
    /// Returns the number of bytes downloaded for this file.
    /// </summary>
    private async Task<long> DownloadFileAsync(
        string repo,
        string fileName,
        string targetPath,
        IProgress<ModelDownloadProgress>? progress,
        string label,
        long cumulativeOffset,
        long? grandTotal,
        CancellationToken cancellationToken)
    {
        var url = BuildUrl(repo, fileName);
        var tempPath = targetPath + ".tmp";

        try
        {
            long downloadedBytes = 0;

            // Scope the streams so the temp file handle is fully released
            // before we move it — otherwise File.Move fails with "being used
            // by another process" (the process itself still holds the handle).
            using (var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken))
            {
                response.EnsureSuccessStatusCode();

                var fileTotalBytes = response.Content.Headers.ContentLength ?? 0;
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None);

                var buffer = new byte[81920];
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, cancellationToken)) > 0)
                {
                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    downloadedBytes += bytesRead;

                    // Report against the combined download when known, otherwise
                    // against this file's size (still offset so the bar advances).
                    var reportedTotal = grandTotal
                        ?? (fileTotalBytes > 0 ? cumulativeOffset + fileTotalBytes : (long?)null);

                    progress?.Report(new ModelDownloadProgress(
                        BytesReceived: cumulativeOffset + downloadedBytes,
                        TotalBytes: reportedTotal));
                }

                await fileStream.FlushAsync(cancellationToken);
            }

            // Atomic move (streams now disposed)
            File.Move(tempPath, targetPath, overwrite: true);
            _logger.LogInformation("Downloaded {Label}: {Path} ({Bytes:N0} bytes)", label, targetPath, downloadedBytes);
            return downloadedBytes;
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
