namespace Parlotype.Core.LlamaServer;

/// <summary>
/// Progress for an install. <see cref="Phase"/> is one of
/// <c>downloading</c>, <c>downloading-companion</c>, <c>verifying</c>,
/// <c>extracting</c>, <c>finalizing</c>.
/// </summary>
public sealed record LlamaServerInstallProgress(
    string Phase,
    long BytesReceived,
    long? TotalBytes)
{
    /// <summary>Fraction in [0, 1], or null when total is unknown.</summary>
    public double? Fraction => TotalBytes is > 0 ? (double)BytesReceived / TotalBytes.Value : null;
}
