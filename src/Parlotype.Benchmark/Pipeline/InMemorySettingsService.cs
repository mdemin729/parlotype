using System.Text.Json;
using Parlotype.Core.Settings;

namespace Parlotype.Benchmark.Pipeline;

/// <summary>
/// In-memory <see cref="ISettingsService"/> for benchmark runs.
/// Pre-populated from <c>LlamaCppConfig</c> so <c>LlamaCppSpeechRecognizer</c> reads
/// benchmark-controlled values rather than the user's personal <c>settings.json</c>.
/// </summary>
internal sealed class InMemorySettingsService : ISettingsService
{
    private readonly Dictionary<string, string?> _values;

    public InMemorySettingsService(Dictionary<string, string?> values) => _values = values;

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
    {
        _values.TryGetValue(key, out var raw);
        if (raw is null)
            return Task.FromResult<T?>(default);

        // Handle the primitive types LlamaCppSpeechRecognizer reads
        if (typeof(T) == typeof(string))
            return Task.FromResult<T?>((T)(object)raw);

        if (typeof(T) == typeof(int) && int.TryParse(raw, out var intVal))
            return Task.FromResult<T?>((T)(object)intVal);

        // Fallback: JSON deserialise
        try
        {
            var result = JsonSerializer.Deserialize<T>(raw);
            return Task.FromResult(result);
        }
        catch
        {
            return Task.FromResult<T?>(default);
        }
    }

    public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
    {
        _values[key] = value is string s ? s : JsonSerializer.Serialize(value);
        return Task.CompletedTask;
    }
}
