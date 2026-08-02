using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>Downloads Whisper GGML models from Hugging Face with progress reporting.</summary>
public sealed class HttpModelDownloadService
{
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);

    private readonly StreamingFileDownloader _downloader;
    private readonly ILogger<HttpModelDownloadService> _logger;

    public HttpModelDownloadService(
        StreamingFileDownloader downloader,
        ILogger<HttpModelDownloadService> logger)
    {
        _downloader = downloader;
        _logger = logger;
    }

    /// <summary>Returns the local cache directory for model files.</summary>
    public static string GetModelCacheDirectory() => AppPaths.Default.ModelsDirectory;

    /// <summary>Returns the expected local file path for a model.</summary>
    public string GetModelPath(WhisperModelType modelType) =>
        Path.Combine(GetModelCacheDirectory(), $"{GetModelFileName(modelType)}.bin");

    /// <summary>Returns true if the model file is already cached locally.</summary>
    public bool IsModelCached(WhisperModelType modelType) =>
        File.Exists(GetModelPath(modelType));

    /// <summary>
    /// Downloads the model with progress reporting. Uses a temp file and
    /// atomic move via <see cref="StreamingFileDownloader"/>.
    /// </summary>
    public async Task DownloadModelAsync(
        WhisperModelType modelType,
        IProgress<ModelDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        var modelPath = GetModelPath(modelType);
        Directory.CreateDirectory(GetModelCacheDirectory());

        await DownloadLock.WaitAsync(cancellationToken);
        try
        {
            // Double-check after acquiring lock
            if (File.Exists(modelPath))
                return;

            var url = GetDownloadUrl(modelType);
            _logger.LogInformation("Downloading Whisper model {ModelType} from {Url}", modelType, url);

            IProgress<StreamingDownloadProgress>? bridged = progress is null
                ? null
                : new Progress<StreamingDownloadProgress>(p =>
                    progress.Report(new ModelDownloadProgress(p.BytesReceived, p.TotalBytes)));

            await _downloader.DownloadAsync(
                url, modelPath, bridged, cancellationToken,
                expectedSha256: WhisperModelInfo.Get(modelType).Sha256);
            _logger.LogInformation("Whisper model {ModelType} download complete (SHA-256 verified)", modelType);
        }
        finally
        {
            DownloadLock.Release();
        }
    }

    private static string GetDownloadUrl(WhisperModelType modelType)
    {
        var modelFileName = GetModelFileName(modelType);
        return $"https://huggingface.co/sandrohanea/whisper.net/resolve/v3/classic/{modelFileName}.bin";
    }

    private static string GetModelFileName(WhisperModelType modelType) => modelType switch
    {
        WhisperModelType.Tiny         => "ggml-tiny",
        WhisperModelType.TinyEn       => "ggml-tiny.en",
        WhisperModelType.Base         => "ggml-base",
        WhisperModelType.BaseEn       => "ggml-base.en",
        WhisperModelType.Small        => "ggml-small",
        WhisperModelType.SmallEn      => "ggml-small.en",
        WhisperModelType.Medium       => "ggml-medium",
        WhisperModelType.MediumEn     => "ggml-medium.en",
        WhisperModelType.LargeV1      => "ggml-large-v1",
        WhisperModelType.LargeV2      => "ggml-large-v2",
        WhisperModelType.LargeV3      => "ggml-large-v3",
        WhisperModelType.LargeV3Turbo => "ggml-large-v3-turbo",
        _ => throw new ArgumentOutOfRangeException(nameof(modelType), modelType, "Unknown Whisper model type"),
    };
}
