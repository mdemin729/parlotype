namespace Parlotype.Core.Speech;

/// <summary>
/// Thrown when a downloaded model file fails its SHA-256 integrity check
/// (security audit 2026-07-11, S2). The offending file is deleted before this
/// is thrown, so a retry re-downloads from scratch.
/// </summary>
public sealed class ModelIntegrityException : InvalidOperationException
{
    /// <summary>File name (not full path) of the artifact that failed verification.</summary>
    public string FileName { get; }

    /// <summary>Expected SHA-256 digest (lowercase hex) from the model catalog.</summary>
    public string ExpectedSha256 { get; }

    /// <summary>Actual SHA-256 digest (lowercase hex) of the downloaded bytes.</summary>
    public string ActualSha256 { get; }

    public ModelIntegrityException(string fileName, string expectedSha256, string actualSha256)
        : base($"Integrity check failed for '{fileName}': expected SHA-256 {expectedSha256} but the " +
               $"download hashed to {actualSha256}. The file was discarded. Retry the download; if it " +
               "keeps failing, the upstream file may have changed or been tampered with.")
    {
        FileName = fileName;
        ExpectedSha256 = expectedSha256;
        ActualSha256 = actualSha256;
    }
}
