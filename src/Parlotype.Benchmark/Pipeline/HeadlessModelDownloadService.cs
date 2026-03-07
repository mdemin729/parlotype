using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;

namespace Parlotype.Benchmark.Pipeline;

/// <summary>
/// Headless implementation of <see cref="IModelDownloadService"/> that downloads
/// models silently without UI prompts. Wraps <see cref="HttpModelDownloadService"/>.
/// </summary>
internal sealed class HeadlessModelDownloadService : IModelDownloadService
{
    private readonly HttpModelDownloadService _http;

    public HeadlessModelDownloadService(HttpModelDownloadService http)
    {
        _http = http;
    }

    public bool IsModelCached(WhisperModelType modelType) => _http.IsModelCached(modelType);

    public async Task<string> EnsureModelAsync(WhisperModelType modelType, CancellationToken cancellationToken = default)
    {
        if (!_http.IsModelCached(modelType))
            await _http.DownloadModelAsync(modelType, null, cancellationToken);
        return _http.GetModelPath(modelType);
    }
}
