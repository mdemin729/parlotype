namespace Parlotype.Core.Settings;

/// <summary>
/// Persists transient window-chrome state (position, size) as key-value pairs.
/// Deliberately separate from <see cref="ISettingsService"/>'s long-lived
/// transcription/model settings — window state changes far more often (e.g.
/// on every drag) and must never share a file or lock with the settings a
/// user configures intentionally (ADR-040).
/// </summary>
public interface IWindowStateService
{
    /// <summary>Gets a window-state value by key, or the default if not found.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default);

    /// <summary>Sets a window-state value by key.</summary>
    Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default);
}
