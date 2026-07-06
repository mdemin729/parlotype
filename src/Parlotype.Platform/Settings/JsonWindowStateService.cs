using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;

namespace Parlotype.Platform.Settings;

/// <summary>
/// Persists transient window-chrome state (position, size) as its own JSON file,
/// separate from <see cref="JsonSettingsService"/>'s long-lived settings file, so
/// frequent state changes (e.g. saving on every window drag) never touch or
/// contend with the settings a user configures intentionally (ADR-040).
/// </summary>
public sealed class JsonWindowStateService : JsonFileStore, IWindowStateService
{
    private static readonly string WindowStatePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "parlotype", "window-state.json");

    public JsonWindowStateService(ILogger<JsonWindowStateService> logger) : base(WindowStatePath, logger)
    {
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
        GetCoreAsync<T>(key, cancellationToken);

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
        SetCoreAsync(key, value, cancellationToken);
}
