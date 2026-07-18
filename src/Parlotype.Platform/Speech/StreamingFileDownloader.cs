using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

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
    /// When <paramref name="expectedSha256"/> is provided, the stream is hashed
    /// while writing and a mismatch throws <see cref="ModelIntegrityException"/>
    /// with the temp file removed — the destination is never touched
    /// (security audit 2026-07-11, S2).
    /// </summary>
    public async Task DownloadAsync(
        string url,
        string destinationPath,
        IProgress<StreamingDownloadProgress>? progress,
        CancellationToken cancellationToken,
        string? expectedSha256 = null)
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
            using var hasher = expectedSha256 is null
                ? null
                : IncrementalHash.CreateHash(HashAlgorithmName.SHA256);

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
                    hasher?.AppendData(buffer, 0, read);
                    received += read;
                    progress?.Report(new StreamingDownloadProgress(received, totalBytes));
                }

                _logger.LogDebug(
                    "Downloaded {Bytes} byte(s) from {Url} to {Path}",
                    received, url, destinationPath);
            }

            if (hasher is not null)
            {
                var actual = Convert.ToHexStringLower(hasher.GetHashAndReset());
                if (!actual.Equals(expectedSha256, StringComparison.OrdinalIgnoreCase))
                    throw new ModelIntegrityException(
                        Path.GetFileName(destinationPath), expectedSha256!.ToLowerInvariant(), actual);
                _logger.LogDebug("SHA-256 verified for {Path}", destinationPath);
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
