using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Verifies <see cref="SpeechRecognizerFactory"/> resolves the recognizer
/// matching the persisted <see cref="SettingsKeys.SpeechEngine"/> setting,
/// including the two opt-in cloud engines (ADR-032).
/// </summary>
public sealed class SpeechRecognizerFactoryTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly Dictionary<string, object?> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Stub satisfying WhisperSpeechRecognizer's constructor dependency. In
    /// production this is supplied by Desktop's composition root
    /// (ModelDownloadDialogService); these tests never call InitializeAsync,
    /// so no method here is exercised.
    /// </summary>
    private sealed class StubModelDownloadService : IModelDownloadService
    {
        public Task<string> EnsureModelAsync(WhisperModelType modelType, CancellationToken cancellationToken = default)
            => throw new NotSupportedException("Not used by these DI-resolution tests.");

        public bool IsModelCached(WhisperModelType modelType) => false;
    }

    private static (SpeechRecognizerFactory Factory, FakeSettingsService Settings) Create()
    {
        var settings = new FakeSettingsService();

        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddPlatformServices();
        // AddPlatformServices() also registers an ISettingsService default
        // (JsonSettingsService); replace it with the in-memory fake so the
        // factory reads engine selection without touching the filesystem.
        services.AddSingleton<ISettingsService>(settings);
        services.AddSingleton<IModelDownloadService, StubModelDownloadService>();

        var provider = services.BuildServiceProvider();
        var factory = new SpeechRecognizerFactory(provider, settings, NullLogger<SpeechRecognizerFactory>.Instance);
        return (factory, settings);
    }

    [Fact]
    public async Task GetRecognizerAsync_Whisper_ResolvesWhisperSpeechRecognizer()
    {
        var (factory, settings) = Create();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString());

        var recognizer = await factory.GetRecognizerAsync();

        Assert.IsType<WhisperSpeechRecognizer>(recognizer);
    }

    [Fact]
    public async Task GetRecognizerAsync_Gemma4_ResolvesLlamaCppSpeechRecognizer()
    {
        var (factory, settings) = Create();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString());

        var recognizer = await factory.GetRecognizerAsync();

        Assert.IsType<LlamaCppSpeechRecognizer>(recognizer);
    }

    [Fact]
    public async Task GetRecognizerAsync_Parakeet_ResolvesParakeetSpeechRecognizer()
    {
        var (factory, settings) = Create();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Parakeet.ToString());

        var recognizer = await factory.GetRecognizerAsync();

        Assert.IsType<ParakeetSpeechRecognizer>(recognizer);
    }

    [Fact]
    public async Task GetRecognizerAsync_OpenAiCompatible_ResolvesOpenAiCompatibleSpeechRecognizer()
    {
        var (factory, settings) = Create();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.OpenAiCompatible.ToString());

        var recognizer = await factory.GetRecognizerAsync();

        Assert.IsType<OpenAiCompatibleSpeechRecognizer>(recognizer);
    }

    [Fact]
    public async Task GetRecognizerAsync_XaiGrok_ResolvesXaiGrokSpeechRecognizer()
    {
        var (factory, settings) = Create();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.XaiGrok.ToString());

        var recognizer = await factory.GetRecognizerAsync();

        Assert.IsType<XaiGrokSpeechRecognizer>(recognizer);
    }

    [Fact]
    public async Task GetRecognizerAsync_UnknownEngine_FallsBackToParakeet()
    {
        var (factory, settings) = Create();
        await settings.SetAsync(SettingsKeys.SpeechEngine, "NotARealEngine");

        var recognizer = await factory.GetRecognizerAsync();

        Assert.IsType<ParakeetSpeechRecognizer>(recognizer);
    }
}
