using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;

namespace Parlotype.WinUI.Services;

/// <summary>
/// WinUI implementation of <see cref="IModelDownloadService"/> that delegates to
/// <see cref="HttpModelDownloadService"/> for the actual download. A ContentDialog
/// with a progress bar can be layered on top in a future iteration.
/// </summary>
public sealed class WinUIModelDownloadDialogService : IModelDownloadService
{
    private readonly HttpModelDownloadService _inner;

    public WinUIModelDownloadDialogService(HttpModelDownloadService inner)
    {
        _inner = inner;
    }

    /// <inheritdoc />
    public bool IsModelCached(WhisperModelType modelType) =>
        _inner.IsModelCached(modelType);

    /// <inheritdoc />
    public async Task<string> EnsureModelAsync(
        WhisperModelType modelType,
        CancellationToken cancellationToken = default)
    {
        var modelPath = _inner.GetModelPath(modelType);

        if (_inner.IsModelCached(modelType))
            return modelPath;

        // TODO: Show a WinUI ContentDialog with a progress bar during download.
        // For now, delegate directly to the platform downloader.
        await _inner.DownloadModelAsync(modelType, progress: null, cancellationToken);
        return modelPath;
    }
}
