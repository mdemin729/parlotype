using Parlotype.Core.Settings;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockSecretStore : ISecretStore
{
    private readonly Dictionary<string, string> _store = new();

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
        Task.FromResult(_store.TryGetValue(key, out var value) ? value : null);

    public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(value))
            _store.Remove(key);
        else
            _store[key] = value;

        return Task.CompletedTask;
    }
}
