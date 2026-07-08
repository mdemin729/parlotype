namespace Parlotype.Core.Speech;

/// <summary>
/// Ensures Parakeet model files are present locally, downloading them when
/// missing. The Platform implementation downloads headlessly (benchmark/CLI);
/// the Desktop implementation shows the shared model-download dialog with a
/// progress bar and a Cancel button (ADR-042), mirroring how
/// <see cref="IModelDownloadService"/> works for Whisper.
/// </summary>
public interface IParakeetModelProvider
{
    /// <summary>
    /// Ensures all files of <paramref name="model"/> are cached locally,
    /// downloading them if needed. Throws <see cref="OperationCanceledException"/>
    /// when the user declines or cancels the download.
    /// </summary>
    Task EnsureModelAsync(ParakeetModelInfo model, CancellationToken cancellationToken = default);
}
