using Parlotype.Core.LlamaServer;
using Parlotype.Platform.LlamaServer;
using Xunit;

namespace Parlotype.Tests.LlamaServer;

public sealed class LlamaServerAssetParserTests
{
    [Theory]
    [InlineData("llama-b9198-bin-win-cpu-x64.zip",       "b9198", LlamaServerBackend.Cpu,    LlamaServerArch.X64)]
    [InlineData("llama-b9198-bin-win-cpu-arm64.zip",     "b9198", LlamaServerBackend.Cpu,    LlamaServerArch.Arm64)]
    [InlineData("llama-b9198-bin-win-vulkan-x64.zip",    "b9198", LlamaServerBackend.Vulkan, LlamaServerArch.X64)]
    [InlineData("llama-b9198-bin-win-sycl-x64.zip",      "b9198", LlamaServerBackend.Sycl,   LlamaServerArch.X64)]
    [InlineData("llama-b9198-bin-win-hip-radeon-x64.zip","b9198", LlamaServerBackend.Hip,    LlamaServerArch.X64)]
    public void Windows_BasicBackends_Parse(string name, string build, LlamaServerBackend backend, LlamaServerArch arch)
    {
        Assert.True(LlamaServerAssetParser.TryParse(name, out var d));
        Assert.Equal(build, d.Build);
        Assert.Equal(backend, d.Backend);
        Assert.Equal(LlamaServerOs.Windows, d.Os);
        Assert.Equal(arch, d.Arch);
        Assert.False(d.IsCompanion);
        Assert.Null(d.CudaVersion);
    }

    [Theory]
    [InlineData("llama-b9198-bin-win-cuda-12.4-x64.zip", "12.4", LlamaServerBackend.Cuda12)]
    [InlineData("llama-b9198-bin-win-cuda-13.1-x64.zip", "13.1", LlamaServerBackend.Cuda13)]
    public void Windows_Cuda_ParsesVersionAndBackend(string name, string version, LlamaServerBackend backend)
    {
        Assert.True(LlamaServerAssetParser.TryParse(name, out var d));
        Assert.Equal(backend, d.Backend);
        Assert.Equal(version, d.CudaVersion);
        Assert.Equal(LlamaServerOs.Windows, d.Os);
        Assert.Equal(LlamaServerArch.X64, d.Arch);
        Assert.False(d.IsCompanion);
    }

    [Theory]
    [InlineData("cudart-llama-bin-win-cuda-12.4-x64.zip", "12.4", LlamaServerBackend.Cuda12)]
    [InlineData("cudart-llama-bin-win-cuda-13.1-x64.zip", "13.1", LlamaServerBackend.Cuda13)]
    public void Companion_Cudart_Parses(string name, string version, LlamaServerBackend backend)
    {
        Assert.True(LlamaServerAssetParser.TryParse(name, out var d));
        Assert.True(d.IsCompanion);
        Assert.Null(d.Build);
        Assert.Equal(version, d.CudaVersion);
        Assert.Equal(backend, d.Backend);
        Assert.Equal(LlamaServerOs.Windows, d.Os);
        Assert.Equal(LlamaServerArch.X64, d.Arch);
    }

    [Theory]
    [InlineData("llama-b9198-bin-macos-arm64.tar.gz",          LlamaServerBackend.Metal,    LlamaServerArch.Arm64)]
    [InlineData("llama-b9198-bin-macos-arm64-kleidiai.tar.gz", LlamaServerBackend.KleidiAi, LlamaServerArch.Arm64)]
    [InlineData("llama-b9198-bin-macos-x64.tar.gz",            LlamaServerBackend.Cpu,      LlamaServerArch.X64)]
    public void MacOs_Parses(string name, LlamaServerBackend backend, LlamaServerArch arch)
    {
        Assert.True(LlamaServerAssetParser.TryParse(name, out var d));
        Assert.Equal(backend, d.Backend);
        Assert.Equal(LlamaServerOs.MacOs, d.Os);
        Assert.Equal(arch, d.Arch);
    }

    [Theory]
    [InlineData("llama-b9198-bin-ubuntu-x64.tar.gz",            LlamaServerBackend.Cpu,    LlamaServerArch.X64)]
    [InlineData("llama-b9198-bin-ubuntu-arm64.tar.gz",          LlamaServerBackend.Cpu,    LlamaServerArch.Arm64)]
    [InlineData("llama-b9198-bin-ubuntu-vulkan-x64.tar.gz",     LlamaServerBackend.Vulkan, LlamaServerArch.X64)]
    [InlineData("llama-b9198-bin-ubuntu-vulkan-arm64.tar.gz",   LlamaServerBackend.Vulkan, LlamaServerArch.Arm64)]
    [InlineData("llama-b9198-bin-ubuntu-rocm-7.2-x64.tar.gz",   LlamaServerBackend.Hip,    LlamaServerArch.X64)]
    [InlineData("llama-b9198-bin-ubuntu-sycl-fp32-x64.tar.gz",  LlamaServerBackend.Sycl,   LlamaServerArch.X64)]
    [InlineData("llama-b9198-bin-ubuntu-sycl-fp16-x64.tar.gz",  LlamaServerBackend.Sycl,   LlamaServerArch.X64)]
    public void Linux_Parses(string name, LlamaServerBackend backend, LlamaServerArch arch)
    {
        Assert.True(LlamaServerAssetParser.TryParse(name, out var d));
        Assert.Equal(backend, d.Backend);
        Assert.Equal(LlamaServerOs.Linux, d.Os);
        Assert.Equal(arch, d.Arch);
    }

    [Fact]
    public void Linux_Openvino_BackendUnknown_StillRecognised()
    {
        Assert.True(LlamaServerAssetParser.TryParse(
            "llama-b9198-bin-ubuntu-openvino-2024.6.0-x64.tar.gz", out var d));
        Assert.Equal(LlamaServerBackend.Unknown, d.Backend);
        Assert.Equal(LlamaServerOs.Linux, d.Os);
        Assert.Equal(LlamaServerArch.X64, d.Arch);
    }

    [Fact]
    public void Linux_S390x_ArchUnknown()
    {
        Assert.True(LlamaServerAssetParser.TryParse(
            "llama-b9198-bin-ubuntu-s390x.tar.gz", out var d));
        Assert.Equal(LlamaServerBackend.Cpu, d.Backend);
        Assert.Equal(LlamaServerOs.Linux, d.Os);
        Assert.Equal(LlamaServerArch.Unknown, d.Arch);
    }

    [Fact]
    public void Android_OsUnknown_SoCatalogFiltersOut()
    {
        Assert.True(LlamaServerAssetParser.TryParse(
            "llama-b9198-bin-android-arm64.tar.gz", out var d));
        Assert.Equal(LlamaServerOs.Unknown, d.Os);
        Assert.Equal(LlamaServerArch.Arm64, d.Arch);
    }

    [Theory]
    [InlineData("llama-b9198-bin-310p-openEuler-x86.tar.gz")]
    [InlineData("llama-b9198-bin-910b-openEuler-aarch64-aclgraph.tar.gz")]
    public void OpenEuler_BackendAndOsUnknown(string name)
    {
        Assert.True(LlamaServerAssetParser.TryParse(name, out var d));
        Assert.Equal(LlamaServerBackend.Unknown, d.Backend);
        Assert.Equal(LlamaServerOs.Unknown, d.Os);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("source-code.zip")]
    [InlineData("llama-b9198-bin-win-cpu-x64.exe")]   // wrong extension
    [InlineData("llama-bnonsense-bin-win-cpu-x64.zip")] // invalid build tag
    [InlineData("llama-b9198-bin-win.zip")]            // too few segments
    [InlineData("not-even-llama.zip")]
    public void UnrecognisedNames_ReturnFalse(string name)
    {
        Assert.False(LlamaServerAssetParser.TryParse(name, out _));
    }

    [Fact]
    public void Companion_NonZipExtension_ReturnsFalse()
    {
        // cudart asset only ships as zip on Windows
        Assert.False(LlamaServerAssetParser.TryParse(
            "cudart-llama-bin-win-cuda-12.4-x64.tar.gz", out _));
    }

    [Fact]
    public void Windows_UnknownBackend_RecognisedAsUnknown()
    {
        // Hypothetical future backend
        Assert.True(LlamaServerAssetParser.TryParse(
            "llama-b9198-bin-win-warpdrive-x64.zip", out var d));
        Assert.Equal(LlamaServerBackend.Unknown, d.Backend);
        Assert.Equal(LlamaServerOs.Windows, d.Os);
    }
}
