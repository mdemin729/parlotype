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
    public void LoadedRuntime_IsNull_BeforeAnyFactoryCreation()
    {
        // Bootstrap sets the order but does NOT create a WhisperFactory,
        // so no native library is loaded yet.
        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Auto, Logger);

        Assert.Null(WhisperRuntimeBootstrap.LoadedRuntime);
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
    public void AutoMode_IncludesCudaAndCpuInOrder()
    {
        WhisperRuntimeBootstrap.Initialize(RuntimePreference.Auto, Logger);

        var order = RuntimeOptions.RuntimeLibraryOrder;

        Assert.Contains(RuntimeLibrary.Cuda, order);
        Assert.Contains(RuntimeLibrary.Cpu, order);
        Assert.Equal(0, order.IndexOf(RuntimeLibrary.Cuda));
    }
}
