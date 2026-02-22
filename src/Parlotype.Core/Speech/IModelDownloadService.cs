namespace Parlotype.Core.Speech;

/// <summary>
/// Ensures a Whisper model is available locally, downloading it if necessary.
/// Desktop implementations may prompt the user for confirmation and show progress.
/// </summary>
public interface IModelDownloadService
{
    /// <summary>
    /// Returns the file path to the requested model, downloading it if not cached.
    /// Implementations may show UI (confirmation dialog, progress bar).
    /// </summary>
    /// <exception cref="OperationCanceledException">The user cancelled the download.</exception>
    Task<string> EnsureModelAsync(WhisperModelType modelType, CancellationToken cancellationToken = default);

    /// <summary>Returns true if the model file is already cached locally.</summary>
    bool IsModelCached(WhisperModelType modelType);
}
