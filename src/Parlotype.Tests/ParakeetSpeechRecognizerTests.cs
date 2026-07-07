using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class ParakeetSpeechRecognizerTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly Dictionary<string, string?> _values = new();

        public FakeSettingsService(params (string Key, string? Value)[] values)
        {
            foreach (var (key, value) in values)
                _values[key] = value;
        }

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_values.TryGetValue(key, out var value) && value is T typed
                ? (T?)typed
                : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _values[key] = value?.ToString();
            return Task.CompletedTask;
        }
    }

    private static ParakeetSpeechRecognizer Create(ISettingsService settings) =>
        new(settings, NullLogger<ParakeetSpeechRecognizer>.Instance);

    [Fact]
    public void IsReady_IsFalseBeforeInitialize()
    {
        var recognizer = Create(new FakeSettingsService());

        Assert.False(recognizer.IsReady);
    }

    [Fact]
    public async Task TranscribeAsync_BeforeInitialize_Throws()
    {
        var recognizer = Create(new FakeSettingsService());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => recognizer.TranscribeAsync(new float[16_000], CancellationToken.None));
    }

    [Fact]
    public async Task ResolveModel_FallsBackToDefault_WhenUnset()
    {
        var recognizer = Create(new FakeSettingsService());

        var model = await recognizer.ResolveModelAsync(CancellationToken.None);

        Assert.Equal(ParakeetModelInfo.Default, model);
    }

    [Fact]
    public async Task ResolveModel_FallsBackToDefault_WhenUnknownId()
    {
        var recognizer = Create(new FakeSettingsService(
            (SettingsKeys.SelectedParakeetModel, "no-such-model")));

        var model = await recognizer.ResolveModelAsync(CancellationToken.None);

        Assert.Equal(ParakeetModelInfo.Default, model);
    }

    [Fact]
    public async Task ResolveModel_HonorsSavedCatalogId()
    {
        var recognizer = Create(new FakeSettingsService(
            (SettingsKeys.SelectedParakeetModel, ParakeetModelInfo.TdtV3Int8.ModelId)));

        var model = await recognizer.ResolveModelAsync(CancellationToken.None);

        Assert.Equal(ParakeetModelInfo.TdtV3Int8, model);
    }

    [Fact]
    public async Task UnloadAsync_WhenNotInitialized_IsNoOp()
    {
        var recognizer = Create(new FakeSettingsService());

        await recognizer.UnloadAsync();

        Assert.False(recognizer.IsReady);
    }

    [Fact]
    public async Task DisposeAsync_ThenUse_Throws()
    {
        var recognizer = Create(new FakeSettingsService());
        await recognizer.DisposeAsync();

        await Assert.ThrowsAsync<ObjectDisposedException>(
            () => recognizer.TranscribeAsync(new float[100], CancellationToken.None));
    }
}
