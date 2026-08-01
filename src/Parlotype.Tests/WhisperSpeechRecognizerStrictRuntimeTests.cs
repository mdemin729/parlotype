using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Verifies that the strict runtime preference (Vulkan) throws
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
        IVulkanEnvironmentProvider vulkan)
        => new(
            new ThrowingDownloadService(),
            settings,
            vulkan,
            NullLogger<WhisperSpeechRecognizer>.Instance);

    [Fact]
    public async Task InitializeAsync_Vulkan_NoLoader_ThrowsRuntimeUnavailable()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.Tiny.ToString());
        await settings.SetAsync(SettingsKeys.RuntimePreference, RuntimePreference.Vulkan.ToString());

        var recognizer = CreateRecognizer(settings, new FakeVulkanProvider(hasLoader: false));

        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(() => recognizer.InitializeAsync());
        Assert.Equal(RuntimePreference.Vulkan, ex.Requested);
    }

    [Fact]
    public async Task InitializeAsync_WithOptions_Vulkan_NoLoader_ThrowsRuntimeUnavailable()
    {
        var settings = new FakeSettingsService();
        var recognizer = CreateRecognizer(settings, new FakeVulkanProvider(hasLoader: false));
        var options = new WhisperOptions { Model = WhisperModelType.Tiny, RuntimePreference = RuntimePreference.Vulkan };

        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(() => recognizer.InitializeAsync(options));
        Assert.Equal(RuntimePreference.Vulkan, ex.Requested);
    }

    /// <summary>
    /// A missing Vulkan loader must not block the non-strict preferences: Auto falls
    /// back to CPU on its own, and Cpu never wanted a GPU in the first place. Both
    /// therefore get as far as the model download, which this fixture refuses.
    /// </summary>
    [Theory]
    [InlineData(RuntimePreference.Auto)]
    [InlineData(RuntimePreference.Cpu)]
    public async Task InitializeAsync_WithOptions_NonStrict_NoLoader_SkipsRuntimeGuard(RuntimePreference preference)
    {
        var settings = new FakeSettingsService();
        var recognizer = CreateRecognizer(settings, new FakeVulkanProvider(hasLoader: false));
        var options = new WhisperOptions { Model = WhisperModelType.Tiny, RuntimePreference = preference };

        await Assert.ThrowsAsync<InvalidOperationException>(() => recognizer.InitializeAsync(options));
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
