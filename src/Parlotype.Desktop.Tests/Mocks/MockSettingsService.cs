using Parlotype.Core.Settings;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockSettingsService : ISettingsService
{
    private readonly Dictionary<string, object?> _store = new();

    /// <summary>
    /// Keys whose writes should silently fail — the value never reaches the store.
    /// Models a disk that accepted the call but did not persist it, which is what
    /// destructive settings have to defend against.
    /// </summary>
    public HashSet<string> FailWritesFor { get; } = [];

    /// <summary>When set, <see cref="SetAsync"/> throws for these keys instead.</summary>
    public HashSet<string> ThrowWritesFor { get; } = [];

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        if (_store.TryGetValue(key, out var value) && value is T typed)
            return Task.FromResult<T?>(typed);
        return Task.FromResult(default(T?));
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        if (ThrowWritesFor.Contains(key))
            throw new IOException($"Simulated write failure for '{key}'");

        if (FailWritesFor.Contains(key))
            return Task.CompletedTask;

        _store[key] = value;
        return Task.CompletedTask;
    }
}
