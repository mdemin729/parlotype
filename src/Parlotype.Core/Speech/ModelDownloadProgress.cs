namespace Parlotype.Core.Speech;

/// <summary>Progress information for a model download operation.</summary>
public sealed record ModelDownloadProgress(
    long BytesReceived,
    long? TotalBytes)
{
    /// <summary>Download progress as a fraction (0.0–1.0), or null if total size is unknown.</summary>
    public double? ProgressFraction => TotalBytes > 0 ? (double)BytesReceived / TotalBytes.Value : null;
}
