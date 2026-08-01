using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Platform;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Whisper.net.LibraryLoader;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Whisper.net resolves its native library once per process, so a
/// <see cref="RuntimePreference"/> change made after the first model load can only
/// be applied by restarting the app (ADR-048). The latch is injected as
/// <see cref="IWhisperRuntimeStatus"/> — mutating Whisper.net's real
/// <see cref="RuntimeOptions.LoadedLibrary"/> static here would break the tests that
/// load actual models in parallel.
/// </summary>
[Collection("WhisperRuntime")]
public sealed class WhisperRuntimeLatchTests : IDisposable
{
    public WhisperRuntimeLatchTests()
    {
        WhisperRuntimeBootstrap.Reset();
    }

    public void Dispose()
    {
        WhisperRuntimeBootstrap.Reset();
    }

    [Theory]
    // Nothing loaded yet — every preference is still satisfiable.
    [InlineData(RuntimePreference.Cuda, null, true)]
    [InlineData(RuntimePreference.Cpu, null, true)]
    // Auto takes whatever won the fallback chain.
    [InlineData(RuntimePreference.Auto, RuntimeLibrary.Cuda, true)]
    [InlineData(RuntimePreference.Auto, RuntimeLibrary.Cpu, true)]
    // Strict GPU preferences must match exactly.
    [InlineData(RuntimePreference.Cuda, RuntimeLibrary.Cuda, true)]
    [InlineData(RuntimePreference.Cuda, RuntimeLibrary.Vulkan, false)]
    [InlineData(RuntimePreference.Vulkan, RuntimeLibrary.Vulkan, true)]
    [InlineData(RuntimePreference.Vulkan, RuntimeLibrary.Cuda, false)]
    // Whisper.net picks between the AVX and no-AVX CPU builds itself.
    [InlineData(RuntimePreference.Cpu, RuntimeLibrary.Cpu, true)]
    [InlineData(RuntimePreference.Cpu, RuntimeLibrary.CpuNoAvx, true)]
    [InlineData(RuntimePreference.Cpu, RuntimeLibrary.Cuda, false)]
    public void IsSatisfiedBy_MatchesPreferenceAgainstLoadedLibrary(
        RuntimePreference preference, RuntimeLibrary? loaded, bool expected)
    {
        Assert.Equal(expected, WhisperRuntimeBootstrap.IsSatisfiedBy(preference, loaded));
    }

    [Fact]
    public async Task InitializeAsync_WithOptions_MismatchedLatch_ThrowsBeforeLoadingModel()
    {
        var recognizer = CreateRecognizer(latched: RuntimeLibrary.Cuda);
        var options = new WhisperOptions { Model = WhisperModelType.Tiny, RuntimePreference = RuntimePreference.Vulkan };

        // ThrowingDownloadService fails the test if the guard runs too late: the
        // point of the pre-check is to not load gigabytes of weights first.
        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(
            () => recognizer.InitializeAsync(options));

        Assert.Equal(RuntimePreference.Vulkan, ex.Requested);
        Assert.True(ex.RequiresRestart);
        Assert.False(recognizer.IsReady);
    }

    [Fact]
    public async Task InitializeAsync_MismatchedLatch_ThrowsBeforeLoadingModel()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedWhisperModel, WhisperModelType.Tiny.ToString());
        await settings.SetAsync(SettingsKeys.RuntimePreference, RuntimePreference.Vulkan.ToString());

        var recognizer = CreateRecognizer(settings, latched: RuntimeLibrary.Cuda);

        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(
            () => recognizer.InitializeAsync());

        Assert.Equal(RuntimePreference.Vulkan, ex.Requested);
        Assert.True(ex.RequiresRestart);
    }

    /// <summary>
    /// The pre-ADR-048 guard only covered Cuda and Vulkan, so picking CPU while a
    /// GPU runtime was latched silently kept running on the GPU.
    /// </summary>
    [Fact]
    public async Task InitializeAsync_WithOptions_CpuRequested_GpuLatched_Throws()
    {
        var recognizer = CreateRecognizer(latched: RuntimeLibrary.Cuda);
        var options = new WhisperOptions { Model = WhisperModelType.Tiny, RuntimePreference = RuntimePreference.Cpu };

        var ex = await Assert.ThrowsAsync<RuntimeUnavailableException>(
            () => recognizer.InitializeAsync(options));

        Assert.Equal(RuntimePreference.Cpu, ex.Requested);
        Assert.True(ex.RequiresRestart);
    }

    /// <summary>
    /// The production status service reads Whisper.net's global; with nothing loaded
    /// it must report "no restart needed" so a fresh process is never blocked.
    /// The preference matching itself is covered by the theory above.
    /// </summary>
    [Fact]
    public void RuntimeStatus_ReportsNoRestart_WhileNothingIsLoaded()
    {
        IWhisperRuntimeStatus status = new WhisperRuntimeStatus();

        if (status.LoadedRuntimeName is not null)
            return; // another test in this assembly already loaded a model

        Assert.False(status.RequiresRestartFor(RuntimePreference.Vulkan));
        Assert.False(status.RequiresRestartFor(RuntimePreference.Cpu));
    }

    /// <summary>
    /// The recognizer and the Settings page both take the latch from DI; a missing
    /// registration would only show up when the app actually records.
    /// </summary>
    [Fact]
    public async Task PlatformDi_ResolvesRuntimeStatus()
    {
        var services = new ServiceCollection();
        services.AddLogging(b => b.SetMinimumLevel(LogLevel.Warning));
        services.AddPlatformServices();
        await using var provider = services.BuildServiceProvider();

        Assert.NotNull(provider.GetRequiredService<IWhisperRuntimeStatus>());
    }

    private static WhisperSpeechRecognizer CreateRecognizer(
        ISettingsService? settings = null,
        RuntimeLibrary? latched = null)
        => new(
            new ThrowingDownloadService(),
            settings ?? new FakeSettingsService(),
            new AlwaysAvailableNvidiaProvider(),
            new AlwaysAvailableVulkanProvider(),
            NullLogger<WhisperSpeechRecognizer>.Instance,
            new FakeRuntimeStatus(latched));

    /// <summary>In-memory stand-in for the process-wide runtime latch.</summary>
    private sealed class FakeRuntimeStatus(RuntimeLibrary? latched) : IWhisperRuntimeStatus
    {
        public string? LoadedRuntimeName => latched?.ToString();

        public bool RequiresRestartFor(RuntimePreference preference)
            => !WhisperRuntimeBootstrap.IsSatisfiedBy(preference, latched);
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

    private sealed class AlwaysAvailableNvidiaProvider : INvidiaEnvironmentProvider
    {
        private static readonly NvidiaEnvironmentInfo Info = new() { DriverVersion = "999.99" };
        public Task<NvidiaEnvironmentInfo> GetAsync(CancellationToken ct = default) => Task.FromResult(Info);
        public Task<NvidiaEnvironmentInfo> RefreshAsync(CancellationToken ct = default) => Task.FromResult(Info);
    }

    private sealed class AlwaysAvailableVulkanProvider : IVulkanEnvironmentProvider
    {
        private static readonly VulkanEnvironmentInfo Info = new() { HasVulkanLoader = true, LoaderVersion = "1.3.0" };
        public Task<VulkanEnvironmentInfo> GetAsync(CancellationToken ct = default) => Task.FromResult(Info);
        public Task<VulkanEnvironmentInfo> RefreshAsync(CancellationToken ct = default) => Task.FromResult(Info);
    }

    /// <summary>Fails the test if the latch guard lets a model download/load start.</summary>
    private sealed class ThrowingDownloadService : IModelDownloadService
    {
        public bool IsModelCached(WhisperModelType modelType) => true;
        public Task<string> EnsureModelAsync(WhisperModelType modelType, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("The model must not be loaded when the runtime is already latched to another backend");
    }
}
