using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>Downloads Whisper GGML models from Hugging Face with progress reporting.</summary>
public sealed class HttpModelDownloadService
{
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);

    private readonly HttpClient _httpClient;
    private readonly ILogger<HttpModelDownloadService> _logger;

    public HttpModelDownloadService(HttpClient httpClient, ILogger<HttpModelDownloadService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    /// <summary>Returns the local cache directory for model files.</summary>
    public static string GetModelCacheDirectory() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "models");

    /// <summary>Returns the expected local file path for a model.</summary>
    public string GetModelPath(WhisperModelType modelType) =>
        Path.Combine(GetModelCacheDirectory(), $"{GetModelFileName(modelType)}.bin");

    /// <summary>Returns true if the model file is already cached locally.</summary>
    public bool IsModelCached(WhisperModelType modelType) =>
        File.Exists(GetModelPath(modelType));

    /// <summary>
    /// Downloads the model with progress reporting.
    /// Uses a temp file and atomic move to prevent partial files.
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

            using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();

            var totalBytes = response.Content.Headers.ContentLength;
            var tempPath = modelPath + ".tmp";

            try
            {
                await using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken);
                await using var fileStream = File.Create(tempPath);

                var buffer = new byte[81_920];
                long bytesReceived = 0;

                while (true)
                {
                    var bytesRead = await contentStream.ReadAsync(buffer, cancellationToken);
                    if (bytesRead == 0)
                        break;

                    await fileStream.WriteAsync(buffer.AsMemory(0, bytesRead), cancellationToken);
                    bytesReceived += bytesRead;

                    progress?.Report(new ModelDownloadProgress(bytesReceived, totalBytes));
                }

                _logger.LogInformation("Download complete: {Bytes} bytes written", bytesReceived);
            }
            catch
            {
                // Clean up temp file on failure
                try { File.Delete(tempPath); } catch { /* best effort */ }
                throw;
            }

            File.Move(tempPath, modelPath);
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
