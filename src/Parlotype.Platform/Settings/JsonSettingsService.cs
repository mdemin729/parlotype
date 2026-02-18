using System.Text.Json;
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

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        await Lock.WaitAsync(cancellationToken);
        try
        {
            var dict = await LoadAsync(cancellationToken);
            if (dict.TryGetValue(key, out var element) && element is JsonElement jsonElement)
            {
                return jsonElement.Deserialize<T>(JsonOptions);
            }
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
            var dict = await LoadAsync(cancellationToken);
            dict[key] = JsonSerializer.SerializeToElement(value, JsonOptions);
            await SaveAsync(dict, cancellationToken);
        }
        finally
        {
            Lock.Release();
        }
    }

    private static async Task<Dictionary<string, object>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(SettingsPath))
            return new Dictionary<string, object>();

        var json = await File.ReadAllTextAsync(SettingsPath, cancellationToken);
        return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions)
               ?? new Dictionary<string, object>();
    }

    private static async Task SaveAsync(Dictionary<string, object> dict, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(dict, JsonOptions);
        await File.WriteAllTextAsync(SettingsPath, json, cancellationToken);
    }
}
