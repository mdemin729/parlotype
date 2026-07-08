using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Downloads Parakeet ONNX model files (encoder + decoder + joiner + tokens)
/// from HuggingFace into the model's cache directory. Also the headless
/// <see cref="IParakeetModelProvider"/> (no dialog) used by the benchmark and
/// tests; the Desktop app overrides the interface registration with a
/// dialog-based provider (ADR-042).
/// </summary>
public sealed class ParakeetModelDownloadService : IParakeetModelProvider
{
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly ILogger<ParakeetModelDownloadService> _logger;

    public ParakeetModelDownloadService(HttpClient httpClient, ILogger<ParakeetModelDownloadService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Returns the local path of a model file.</summary>
    public static string GetFilePath(ParakeetModelInfo model, string fileName) =>
        Path.Combine(model.GetModelDirectory(), fileName);

    /// <summary>Returns true when all four model files are cached locally.</summary>
    public bool IsModelCached(ParakeetModelInfo model) =>
        model.FileNames.All(f => File.Exists(GetFilePath(model, f)));

    /// <summary>Headless ensure: downloads missing files without any UI.</summary>
    public Task EnsureModelAsync(ParakeetModelInfo model, CancellationToken cancellationToken = default) =>
        IsModelCached(model)
            ? Task.CompletedTask
            : DownloadModelAsync(model, progress: null, cancellationToken);

    /// <summary>
    /// Downloads any missing model files. Reports a single cumulative progress
    /// across all files so the UI shows one combined "X / total" figure rather
    /// than resetting per file.
    /// </summary>
    public async Task DownloadModelAsync(
        ParakeetModelInfo model,
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(model.GetModelDirectory());

        await DownloadLock.WaitAsync(cancellationToken);
        try
        {
            var pending = model.FileNames
                .Where(f => !File.Exists(GetFilePath(model, f)))
                .ToList();

            // Pre-compute the combined size via HEAD so progress is reported
            // against the whole download, not each file. If any size is
            // unknown we fall back to per-file totals (offset + current).
            var grandTotal = 0L;
            var totalKnown = true;
            foreach (var fileName in pending)
            {
                var size = await GetContentLengthAsync(BuildUrl(model.HuggingFaceRepo, fileName), cancellationToken);
                if (size is > 0)
                    grandTotal += size.Value;
                else
                    totalKnown = false;
            }

            var completedBytes = 0L;
            foreach (var fileName in pending)
            {
                _logger.LogInformation("Downloading Parakeet file: {FileName}", fileName);
                var fileBytes = await DownloadFileAsync(
                    model.HuggingFaceRepo, fileName, GetFilePath(model, fileName), progress,
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

    /// <summary>Deletes all cached files (and the model directory) if present.</summary>
    public async Task DeleteModelAsync(ParakeetModelInfo model)
    {
        await DownloadLock.WaitAsync();
        try
        {
            foreach (var fileName in model.FileNames)
            {
                var path = GetFilePath(model, fileName);
                if (File.Exists(path))
                {
                    File.Delete(path);
                    _logger.LogInformation("Deleted Parakeet model file: {Path}", path);
                }
            }

            var dir = model.GetModelDirectory();
            if (Directory.Exists(dir) && !Directory.EnumerateFileSystemEntries(dir).Any())
                Directory.Delete(dir);
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
            _logger.LogInformation("Downloaded {FileName}: {Path} ({Bytes:N0} bytes)", fileName, targetPath, downloadedBytes);
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
