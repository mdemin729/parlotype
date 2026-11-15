namespace Parlotype.Core.Speech;

/// <summary>
/// Builds the download URL of a file in a Hugging Face model repo, always at
/// one commit (ADR-076). A branch such as <c>main</c> is refused: upstream can
/// replace the files behind a branch at any time, and the catalog SHA-256
/// values then stop matching what the URL serves.
/// </summary>
public static class HuggingFaceUrl
{
    private const int CommitShaLength = 40;

    /// <summary>
    /// Returns <c>https://huggingface.co/{repo}/resolve/{revision}/{filePath}</c>.
    /// </summary>
    /// <param name="repo">Repo id, <c>owner/name</c>.</param>
    /// <param name="revision">Full 40-character lowercase commit SHA.</param>
    /// <param name="filePath">Path of the file inside the repo, with forward slashes.</param>
    /// <exception cref="ArgumentException">
    /// <paramref name="revision"/> is not a full commit SHA, or another argument is empty.
    /// </exception>
    public static string Resolve(string repo, string revision, string filePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(repo);
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        if (!IsCommitSha(revision))
        {
            throw new ArgumentException(
                $"Hugging Face revision must be a full 40-character lowercase commit SHA, not a branch or tag: '{revision}'",
                nameof(revision));
        }

        return $"https://huggingface.co/{repo}/resolve/{revision}/{filePath}";
    }

    /// <summary>True when <paramref name="revision"/> is a full lowercase commit SHA.</summary>
    public static bool IsCommitSha(string? revision) =>
        revision is { Length: CommitShaLength }
        && revision.All(c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f'));
}
