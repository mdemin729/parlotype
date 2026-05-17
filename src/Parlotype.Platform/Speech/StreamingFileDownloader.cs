using Microsoft.Extensions.Logging;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Streams an HTTP GET to a local file using an atomic temp-file pattern:
/// the body is written to <c>{dest}.tmp</c> in chunks and moved into place
/// on success. On any failure the temp file is removed so callers never see
/// a half-written destination. Shared by Whisper model downloads and
/// llama-server installs to avoid duplicating the loop.
/// </summary>
public sealed class StreamingFileDownloader
{
    private const int BufferSize = 81_920;

    private readonly HttpClient _httpClient;
    private readonly ILogger<StreamingFileDownloader> _logger;

    public StreamingFileDownloader(HttpClient httpClient, ILogger<StreamingFileDownloader> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>
    /// Downloads <paramref name="url"/> to <paramref name="destinationPath"/>.
    /// Throws on non-2xx status. Throws <see cref="OperationCanceledException"/>
    /// when the token cancels. The destination directory is created if needed.
    /// </summary>
    public async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<StreamingDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(destinationPath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        var tempPath = destinationPath + ".tmp";

        using var response = await _httpClient.GetAsync(
            url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength;

        try
        {
            await using (var content = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var file = File.Create(tempPath))
            {
                var buffer = new byte[BufferSize];
                long received = 0;
                while (true)
                {
                    var read = await content.ReadAsync(buffer, cancellationToken);
                    if (read == 0) break;
                    await file.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                    received += read;
                    progress?.Report(new StreamingDownloadProgress(received, totalBytes));
                }

                _logger.LogDebug(
                    "Downloaded {Bytes} byte(s) from {Url} to {Path}",
                    received, url, destinationPath);
            }

            if (File.Exists(destinationPath))
                File.Delete(destinationPath);
            File.Move(tempPath, destinationPath);
        }
        catch
        {
            try { if (File.Exists(tempPath)) File.Delete(tempPath); }
            catch { /* best effort */ }
            throw;
        }
    }
}
