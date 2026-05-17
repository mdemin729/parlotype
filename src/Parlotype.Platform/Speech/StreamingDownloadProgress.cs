namespace Parlotype.Platform.Speech;

/// <summary>Streaming-download progress for <see cref="StreamingFileDownloader"/>.</summary>
public sealed record StreamingDownloadProgress(long BytesReceived, long? TotalBytes)
{
    /// <summary>Fraction in [0, 1], or null when total is unknown.</summary>
    public double? Fraction => TotalBytes is > 0 ? (double)BytesReceived / TotalBytes.Value : null;
}
