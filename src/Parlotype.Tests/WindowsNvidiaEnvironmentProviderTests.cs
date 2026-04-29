using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public sealed class WindowsNvidiaEnvironmentProviderTests
{
    [Fact]
    public void ParseNvidiaSmiOutput_ExtractsDriverAndCudaVersions()
    {
        const string output = """
            +-----------------------------------------------------------------------------------------+
            | NVIDIA-SMI 596.36                 Driver Version: 596.36         CUDA Version: 13.2     |
            +-----------------------------------------+------------------------+----------------------+
            """;

        var (driver, cuda) = WindowsNvidiaEnvironmentProvider.ParseNvidiaSmiOutput(output);

        Assert.Equal("596.36", driver);
        Assert.Equal("13.2", cuda);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_ReturnsNulls_WhenNoMatches()
    {
        var (driver, cuda) = WindowsNvidiaEnvironmentProvider.ParseNvidiaSmiOutput("garbage output");

        Assert.Null(driver);
        Assert.Null(cuda);
    }

    [Fact]
    public void ParseNvidiaSmiOutput_HandlesMissingCudaLine()
    {
        const string output = "Driver Version: 555.99";
        var (driver, cuda) = WindowsNvidiaEnvironmentProvider.ParseNvidiaSmiOutput(output);

        Assert.Equal("555.99", driver);
        Assert.Null(cuda);
    }

    [Theory]
    [InlineData(@"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v13.2", "13.2")]
    [InlineData(@"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA\v12.9\", "12.9")]
    [InlineData(@"C:\custom\v11.8", "11.8")]
    public void ExtractVersionFromCudaPath_ReturnsVersion(string path, string expected)
    {
        Assert.Equal(expected, WindowsNvidiaEnvironmentProvider.ExtractVersionFromCudaPath(path));
    }

    [Theory]
    [InlineData(@"C:\Program Files\NVIDIA GPU Computing Toolkit\CUDA")]
    [InlineData(@"C:\not-a-cuda-path")]
    [InlineData(@"")]
    [InlineData(@"v")]  // no digit after v
    [InlineData(@"version")]
    public void ExtractVersionFromCudaPath_ReturnsNull_ForUnrecognizedPaths(string path)
    {
        Assert.Null(WindowsNvidiaEnvironmentProvider.ExtractVersionFromCudaPath(path));
    }

    [Theory]
    [InlineData(13020, "13.2")]
    [InlineData(12090, "12.9")]
    [InlineData(11080, "11.8")]
    [InlineData(10000, "10.0")]
    [InlineData(13000, "13.0")]
    public void DecodeCudaVersion_ReturnsExpectedString(int raw, string expected)
    {
        Assert.Equal(expected, WindowsNvidiaEnvironmentProvider.DecodeCudaVersion(raw));
    }

    [Fact]
    public async Task GetAsync_CachesResult()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var provider = new WindowsNvidiaEnvironmentProvider(NullLogger<WindowsNvidiaEnvironmentProvider>.Instance);

        var first = await provider.GetAsync();
        var second = await provider.GetAsync();

        Assert.Same(first, second);
    }

    [Fact]
    public async Task RefreshAsync_ProducesNewSnapshot()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var provider = new WindowsNvidiaEnvironmentProvider(NullLogger<WindowsNvidiaEnvironmentProvider>.Instance);

        var first = await provider.GetAsync();
        var refreshed = await provider.RefreshAsync();

        // Refresh always replaces the cache with a fresh instance.
        Assert.NotSame(first, refreshed);
        // But the contents should be equivalent on a stable system.
        Assert.Equal(first.DriverVersion, refreshed.DriverVersion);
        Assert.Equal(first.DriverMaxCudaVersion, refreshed.DriverMaxCudaVersion);
        Assert.Equal(first.InstalledToolkitVersions, refreshed.InstalledToolkitVersions);
    }

    [Fact]
    public void NvidiaEnvironmentInfo_Empty_HasNoNvidia()
    {
        Assert.False(NvidiaEnvironmentInfo.Empty.HasNvidia);
        Assert.Null(NvidiaEnvironmentInfo.Empty.DriverVersion);
        Assert.Empty(NvidiaEnvironmentInfo.Empty.InstalledToolkitVersions);
        Assert.Empty(NvidiaEnvironmentInfo.Empty.LoadableRuntimes);
    }

    [Fact]
    public void NvidiaEnvironmentInfo_HasNvidia_TrueWhenDriverPresent()
    {
        var info = new NvidiaEnvironmentInfo { DriverVersion = "596.36" };
        Assert.True(info.HasNvidia);
    }

    [Fact]
    public void NvidiaEnvironmentInfo_HasNvidia_TrueWhenToolkitsPresent()
    {
        var info = new NvidiaEnvironmentInfo { InstalledToolkitVersions = ["13.2"] };
        Assert.True(info.HasNvidia);
    }
}
