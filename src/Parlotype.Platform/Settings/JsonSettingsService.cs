using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.Settings;

/// <summary>
/// Persists long-lived application settings (transcription, model, hotkey, etc.)
/// as a JSON file in the local application data folder.
/// </summary>
public sealed class JsonSettingsService : JsonFileStore, ISettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "parlotype", "settings.json");

    public JsonSettingsService(ILogger<JsonSettingsService> logger) : base(SettingsPath, logger)
    {
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        GetCoreAsync<T>(key, cancellationToken);

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
        SetCoreAsync(key, value, cancellationToken);
}
