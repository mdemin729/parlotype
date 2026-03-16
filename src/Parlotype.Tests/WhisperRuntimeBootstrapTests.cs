using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Whisper.net.LibraryLoader;
using Xunit;

namespace Parlotype.Tests;

[Collection("WhisperRuntime")]
public sealed class WhisperRuntimeBootstrapTests : IDisposable
{
    private static readonly ILogger Logger = NullLogger.Instance;

    public WhisperRuntimeBootstrapTests()
    {
        WhisperRuntimeBootstrap.Reset();
    }

    public void Dispose()
    {
        WhisperRuntimeBootstrap.Reset();
    }

    [Fact]
    public void Initialize_Auto_SetsOrderToCudaThenCpu()
    {
        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Auto, Logger);

        Assert.Equal(
            [RuntimeLibrary.Cuda, RuntimeLibrary.Cpu],
            RuntimeOptions.RuntimeLibraryOrder);
    }

    [Fact]
    public void Initialize_Cpu_SetsOrderToCpuOnly()
    {
        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Cpu, Logger);

        Assert.Equal(
            [RuntimeLibrary.Cpu],
            RuntimeOptions.RuntimeLibraryOrder);
    }

    [Fact]
    public void Initialize_IsIdempotent_SecondCallIgnored()
    {
        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Cpu, Logger);
        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Auto, Logger);

        // First call wins — should still be Cpu-only.
        Assert.Equal(
            [RuntimeLibrary.Cpu],
            RuntimeOptions.RuntimeLibraryOrder);
    }

    [Fact]
    public async Task EnsureInitializedAsync_ReadsSetting_Auto()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.RuntimePreference, "Auto");

        await WhisperRuntimeBootstrap.EnsureInitializedAsync(settings, Logger);

        Assert.Equal(
            [RuntimeLibrary.Cuda, RuntimeLibrary.Cpu],
            RuntimeOptions.RuntimeLibraryOrder);
    }

    [Fact]
    public async Task EnsureInitializedAsync_ReadsSetting_Cpu()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.RuntimePreference, "Cpu");

        await WhisperRuntimeBootstrap.EnsureInitializedAsync(settings, Logger);

        Assert.Equal(
            [RuntimeLibrary.Cpu],
            RuntimeOptions.RuntimeLibraryOrder);
    }

    [Fact]
    public async Task EnsureInitializedAsync_MissingSetting_DefaultsToAuto()
    {
        var settings = new FakeSettingsService(); // no key stored

        await WhisperRuntimeBootstrap.EnsureInitializedAsync(settings, Logger);

        Assert.Equal(
            [RuntimeLibrary.Cuda, RuntimeLibrary.Cpu],
            RuntimeOptions.RuntimeLibraryOrder);
    }

    [Fact]
    public async Task EnsureInitializedAsync_InvalidSetting_DefaultsToAuto()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.RuntimePreference, "garbage");

        await WhisperRuntimeBootstrap.EnsureInitializedAsync(settings, Logger);

        Assert.Equal(
            [RuntimeLibrary.Cuda, RuntimeLibrary.Cpu],
            RuntimeOptions.RuntimeLibraryOrder);
    }

    [Fact]
    public void IsInitialized_FalseBeforeInit_TrueAfter()
    {
        Assert.False(WhisperRuntimeBootstrap.IsInitialized);

        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Auto, Logger);

        Assert.True(WhisperRuntimeBootstrap.IsInitialized);
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
}
