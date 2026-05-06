using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Verifies that strict runtime preferences (Cuda, Vulkan) throw
/// <see cref="RuntimeUnavailableException"/> on a host where the corresponding
/// environment is not detected, instead of silently falling back to CPU.
/// </summary>
[Collection("WhisperRuntime")]
public sealed class WhisperSpeechRecognizerStrictRuntimeTests : IDisposable
{
    public WhisperSpeechRecognizerStrictRuntimeTests()
    {
        WhisperRuntimeBootstrap.Reset();
    }

    public void Dispose()
    {
        WhisperRuntimeBootstrap.Reset();
    }

    private static WhisperSpeechRecognizer CreateRecognizer(
        ISettingsService settings,
        INvidiaEnvironmentProvider nvidia,
        IVulkanEnvironmentProvider vulkan)
        => new(
            new ThrowingDownloadService(),
            settings,
            nvidia,
            vulkan,
            NullLogger<WhisperSpeechRecognizer>.Instance);

    [Fact]
    public async Task InitializeAsync_Cuda_NoNvidia_ThrowsRuntimeUnavailable()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.Tiny.ToString());
        await settings.SetAsync(SettingsKeys.RuntimePreference, RuntimePreference.Cuda.ToString());

        var recognizer = CreateRecognizer(settings, new FakeNvidiaProvider(hasNvidia: false), new FakeVulkanProvider(hasLoader: true));

        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(() => recognizer.InitializeAsync());
        Assert.Equal(RuntimePreference.Cuda, ex.Requested);
    }

    [Fact]
    public async Task InitializeAsync_Vulkan_NoLoader_ThrowsRuntimeUnavailable()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.Tiny.ToString());
        await settings.SetAsync(SettingsKeys.RuntimePreference, RuntimePreference.Vulkan.ToString());

        var recognizer = CreateRecognizer(settings, new FakeNvidiaProvider(hasNvidia: true), new FakeVulkanProvider(hasLoader: false));

        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(() => recognizer.InitializeAsync());
        Assert.Equal(RuntimePreference.Vulkan, ex.Requested);
    }

    [Fact]
    public async Task InitializeAsync_WithOptions_Cuda_NoNvidia_ThrowsRuntimeUnavailable()
    {
        var settings = new FakeSettingsService();
        var recognizer = CreateRecognizer(settings, new FakeNvidiaProvider(hasNvidia: false), new FakeVulkanProvider(hasLoader: true));
        var options = new WhisperOptions { Model = WhisperModelType.Tiny, RuntimePreference = RuntimePreference.Cuda };

        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(() => recognizer.InitializeAsync(options));
        Assert.Equal(RuntimePreference.Cuda, ex.Requested);
    }

    [Fact]
    public async Task InitializeAsync_WithOptions_Vulkan_NoLoader_ThrowsRuntimeUnavailable()
    {
        var settings = new FakeSettingsService();
        var recognizer = CreateRecognizer(settings, new FakeNvidiaProvider(hasNvidia: true), new FakeVulkanProvider(hasLoader: false));
        var options = new WhisperOptions { Model = WhisperModelType.Tiny, RuntimePreference = RuntimePreference.Vulkan };

        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(() => recognizer.InitializeAsync(options));
        Assert.Equal(RuntimePreference.Vulkan, ex.Requested);
    }

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

    private sealed class FakeNvidiaProvider(bool hasNvidia) : INvidiaEnvironmentProvider
    {
        private readonly NvidiaEnvironmentInfo _info = hasNvidia
            ? new NvidiaEnvironmentInfo { DriverVersion = "999.99" }
            : NvidiaEnvironmentInfo.Empty;
        public Task<NvidiaEnvironmentInfo> GetAsync(CancellationToken ct = default) => Task.FromResult(_info);
        public Task<NvidiaEnvironmentInfo> RefreshAsync(CancellationToken ct = default) => Task.FromResult(_info);
    }

    private sealed class FakeVulkanProvider(bool hasLoader) : IVulkanEnvironmentProvider
    {
        private readonly VulkanEnvironmentInfo _info = hasLoader
            ? new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" }
            : VulkanEnvironmentInfo.Empty;
        public Task<VulkanEnvironmentInfo> GetAsync(CancellationToken ct = default) => Task.FromResult(_info);
        public Task<VulkanEnvironmentInfo> RefreshAsync(CancellationToken ct = default) => Task.FromResult(_info);
    }

    /// <summary>
    /// Throws if asked to ensure a model. The strict-runtime guard should fail
    /// before model download is attempted, so this should never be called.
    /// </summary>
    private sealed class ThrowingDownloadService : IModelDownloadService
    {
        public bool IsModelCached(WhisperModelType modelType) => true;
        public Task<string> EnsureModelAsync(WhisperModelType modelType, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("EnsureModelAsync should not be called when runtime is unavailable");
    }
}
