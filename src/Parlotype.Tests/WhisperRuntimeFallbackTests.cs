using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Whisper.net.LibraryLoader;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Validates the bootstrap + factory fallback path for machines without NVIDIA GPU.
/// Serialized with <see cref="WhisperRuntimeBootstrapTests"/> via the shared collection.
/// </summary>
[Collection("WhisperRuntime")]
public sealed class WhisperRuntimeFallbackTests : IDisposable
{
    private static readonly ILogger Logger = NullLogger.Instance;

    public WhisperRuntimeFallbackTests()
    {
        WhisperRuntimeBootstrap.Reset();
    }

    public void Dispose()
    {
        WhisperRuntimeBootstrap.Reset();
    }

    [Fact]
    public void Initialize_DoesNotLoadANativeLibrary()
    {
        // Bootstrap sets the order but does NOT create a WhisperFactory, so it
        // cannot change what is loaded. Compared against the value before the call
        // rather than null: another class in this collection may already have
        // loaded a model, and that latch is process-wide for the whole test run.
        var before = WhisperRuntimeBootstrap.LoadedRuntime;

        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Auto, Logger);

        Assert.Equal(before, WhisperRuntimeBootstrap.LoadedRuntime);
    }

    [Fact]
    public void CpuOnlyMode_ExcludesCudaFromOrder()
    {
        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Cpu, Logger);

        var order = RuntimeOptions.RuntimeLibraryOrder;

        Assert.Equal([RuntimeLibrary.Cpu], order);
        Assert.DoesNotContain(RuntimeLibrary.Cuda, order);
    }

    [Fact]
    public void AutoMode_IncludesCudaVulkanAndCpuInOrder()
    {
        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Auto, Logger);

        var order = RuntimeOptions.RuntimeLibraryOrder;

        Assert.Contains(RuntimeLibrary.Cuda, order);
        Assert.Contains(RuntimeLibrary.Vulkan, order);
        Assert.Contains(RuntimeLibrary.Cpu, order);
        Assert.Equal(0, order.IndexOf(RuntimeLibrary.Cuda));
        Assert.Equal(1, order.IndexOf(RuntimeLibrary.Vulkan));
    }
}
