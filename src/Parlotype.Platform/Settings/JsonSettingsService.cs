using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.Settings;

/// <summary>
/// Persists long-lived application settings (transcription, model, hotkey, etc.)
/// as a JSON file under <see cref="IAppPaths.SettingsDirectory"/>.
/// </summary>
public sealed class JsonSettingsService : JsonFileStore, ISettingsService
{
    public JsonSettingsService(IAppPaths paths, ILogger<JsonSettingsService> logger)
        : base(paths.SettingsFilePath, logger)
    {
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        GetCoreAsync<T>(key, cancellationToken);

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
        SetCoreAsync(key, value, cancellationToken);
}
