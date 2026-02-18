namespace Parlotype.Core.Settings;

/// <summary>
/// Persists application settings as key-value pairs.
/// </summary>
public interface ISettingsService
{
    /// <summary>Gets a setting value by key, or the default if not found.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Sets a setting value by key.</summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}
