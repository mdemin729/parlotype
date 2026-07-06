using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace Parlotype.Platform.Settings;

/// <summary>
/// Generic JSON key-value file persistence shared by <see cref="JsonSettingsService"/>
/// and <see cref="JsonWindowStateService"/>. Each subclass points at its own file and
/// gets its own lock, so unrelated persistence concerns — long-lived transcription/
/// model settings vs. transient window-chrome state — never contend with or corrupt
/// each other's file (ADR-040).
/// </summary>
public abstract class JsonFileStore
{
    private readonly string _path;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private readonly ILogger _logger;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    protected JsonFileStore(string path, ILogger logger)
    {
        _path = path;
        _logger = logger;
    }

    protected async Task<T?> GetCoreAsync<T>(string key, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Reading key: {Key}", key);
            var dict = await LoadAsync(cancellationToken);
            if (dict.TryGetValue(key, out var element) && element is JsonElement jsonElement)
            {
                var result = jsonElement.Deserialize<T>(JsonOptions);
                _logger.LogDebug("Value: {Key} = {Value}", key, result);
                return result;
            }
            _logger.LogDebug("Key not found: {Key}, using default", key);
            return default;
        }
        finally
        {
            _lock.Release();
        }
    }

    protected async Task SetCoreAsync<T>(string key, T value, CancellationToken cancellationToken)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            _logger.LogDebug("Writing key: {Key} = {Value}", key, value);
            var dict = await LoadAsync(cancellationToken);
            dict[key] = JsonSerializer.SerializeToElement(value, JsonOptions);
            await SaveAsync(dict, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    private async Task<Dictionary<string, object>> LoadAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(_path))
            return new Dictionary<string, object>();

        try
        {
            var json = await File.ReadAllTextAsync(_path, cancellationToken);
            return JsonSerializer.Deserialize<Dictionary<string, object>>(json, JsonOptions)
                   ?? new Dictionary<string, object>();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load file: {Path}", _path);
            return new Dictionary<string, object>();
        }
    }

    private async Task SaveAsync(Dictionary<string, object> dict, CancellationToken cancellationToken)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);

        var json = JsonSerializer.Serialize(dict, JsonOptions);
        await File.WriteAllTextAsync(_path, json, cancellationToken);
    }
}
