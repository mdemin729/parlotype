using Parlotype.Core.Settings;

namespace Parlotype.Desktop.V2.Tests.Mocks;

public sealed class MockSettingsService : ISettingsService
{
    private readonly Dictionary<string, object?> _store = new();

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value) && value is T typed)
            return Task.FromResult<T?>(typed);
        return Task.FromResult(default(T?));
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        _store[key] = value;
        return Task.CompletedTask;
    }
}
