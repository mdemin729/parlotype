using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;

namespace Parlotype.Desktop.V2.Services;

/// <summary>
/// V2 implementation of <see cref="IModelDownloadService"/> that downloads Whisper
/// models silently in the background. The V2 frontend is tray-first and has no
/// always-visible main window to host a confirmation dialog, so downloads proceed
/// without UI prompts. Progress is logged.
/// </summary>
public sealed class SilentModelDownloadService : IModelDownloadService
{
    private readonly HttpModelDownloadService _http;
    private readonly ILogger<SilentModelDownloadService> _logger;

    public SilentModelDownloadService(HttpModelDownloadService http, ILogger<SilentModelDownloadService> logger)
    {
        _http = http;
        _logger = logger;
    }

    public bool IsModelCached(WhisperModelType modelType) => _http.IsModelCached(modelType);

    public async Task<string> EnsureModelAsync(WhisperModelType modelType, CancellationToken cancellationToken = default)
    {
        if (_http.IsModelCached(modelType))
            return _http.GetModelPath(modelType);

        var info = WhisperModelInfo.Get(modelType);
        _logger.LogInformation("Downloading Whisper model {Model} ({Size})…", info.DisplayName, info.DiskSize);

        var progress = new Progress<ModelDownloadProgress>(p =>
        {
            if (p.ProgressFraction is { } fraction)
                _logger.LogDebug("Model {Model} download {Percent:F0}%", info.DisplayName, fraction * 100);
        });

        await _http.DownloadModelAsync(modelType, progress, cancellationToken);
        _logger.LogInformation("Model {Model} downloaded", info.DisplayName);
        return _http.GetModelPath(modelType);
    }
}
