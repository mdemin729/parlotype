using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.Settings;

/// <summary>
/// Persists settings as a JSON file in the local application data folder.
/// </summary>
public sealed class JsonSettingsService : ISettingsService
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "parlotype", "settings.json");

    private static readonly SemaphoreSlim Lock = new(1, 1);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<JsonSettingsService> _logger;

    public JsonSettingsService(ILogger<JsonSettingsService> logger)
    {
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        await Lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Reading setting: {Key}", key);
            var dict = await LoadAsync(cancellationToken);
            if (dict.TryGetValue(key, out var element) && element is JsonElement jsonElement)
            {
                var result = jsonElement.Deserialize<T>(JsonOptions);
                _logger.LogDebug("Setting value: {Key} = {Value}", key, result);
                return result;
            }
            _logger.LogDebug("Setting not found: {Key}, using default", key);
            return default;
        }
        finally
        {
            Lock.Release();
        }
    }

    public async Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        await Lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Writing setting: {Key} = {Value}", key, value);
            var dict = await LoadAsync(cancellationToken);
            dict[key] = JsonSerializer.SerializeToElement(value, JsonOptions);
            await SaveAsync(dict, cancellationToken);
        }
        finally
        {
            Lock.Release();
        }
    }

    private async Task<Dictionary<string, object>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SettingsPath))
            return new Dictionary<string, object>();

        try
        {
            var json = await File.ReadAllTextAsync(SettingsPath, cancellationToken);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions)
                   ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load settings file");
            return new Dictionary<string, object>();
        }
    }

    private static async Task SaveAsync(Dictionary<string, object> dict, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(dict, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json, cancellationToken);
    }
}
