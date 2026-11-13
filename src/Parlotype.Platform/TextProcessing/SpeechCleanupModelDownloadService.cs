using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextProcessing;
using Parlotype.Platform.Speech;

namespace Parlotype.Platform.TextProcessing;

/// <summary>
/// Downloads and deletes speech cleanup models (ADR-073). One GGUF file per
/// model, fetched from HuggingFace into the shared model cache and verified
/// against the catalog SHA-256 before it appears at its destination.
/// </summary>
public sealed class SpeechCleanupModelDownloadService
{
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);

    private readonly StreamingFileDownloader _downloader;
    private readonly IAppPaths _paths;
    private readonly ILogger<SpeechCleanupModelDownloadService> _logger;

    public SpeechCleanupModelDownloadService(
        StreamingFileDownloader downloader,
        IAppPaths paths,
        ILogger<SpeechCleanupModelDownloadService> logger)
    {
        _downloader = downloader;
        _paths = paths;
        _logger = logger;
    }

    /// <summary>Where the model's GGUF lives once downloaded.</summary>
    public string GetModelPath(SpeechCleanupModelInfo model) =>
        Path.Combine(_paths.ModelsDirectory, model.GgufFileName);

    public bool IsModelCached(SpeechCleanupModelInfo model) => File.Exists(GetModelPath(model));

    /// <summary>Downloads the model if it is not already cached.</summary>
    public async Task DownloadModelAsync(
        SpeechCleanupModelInfo model,
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        await DownloadLock.WaitAsync(cancellationToken);
        try
        {
            if (IsModelCached(model))
                return;

            var url = $"https://huggingface.co/{model.HuggingFaceRepo}/resolve/main/{model.GgufFileName}";
            _logger.LogInformation("Downloading speech cleanup model {ModelId}", model.ModelId);

            var streamProgress = progress is null
                ? null
                : new Progress<StreamingDownloadProgress>(
                    p => progress.Report(new ModelDownloadProgress(p.BytesReceived, p.TotalBytes)));

            await _downloader.DownloadAsync(
                url, GetModelPath(model), streamProgress, cancellationToken, model.GgufSha256);
        }
        finally
        {
            DownloadLock.Release();
        }
    }

    /// <summary>
    /// Deletes the model file if present. The caller stops the model process
    /// first; a running server holds the file open.
    /// </summary>
    public async Task DeleteModelAsync(SpeechCleanupModelInfo model)
    {
        await DownloadLock.WaitAsync();
        try
        {
            var path = GetModelPath(model);
            if (File.Exists(path))
            {
                File.Delete(path);
                _logger.LogInformation("Deleted speech cleanup model file: {Path}", path);
            }
        }
        finally
        {
            DownloadLock.Release();
        }
    }
}
