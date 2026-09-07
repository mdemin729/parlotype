using Parlotype.Core.Settings;
using Xunit;

namespace Parlotype.Tests;

/// <summary>
/// Guards the detector that tells a user their manual <c>llama-server</c> folder is
/// sitting in the folder Velopack deletes on uninstall (ADR-065).
/// </summary>
public class VelopackPackFolderTests
{
    private static string LocalAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    [Fact]
    public void Root_IsThePackFolder_OnWindowsOnly()
    {
        if (OperatingSystem.IsWindows())
            Assert.Equal(Path.Combine(LocalAppData, "Parlotype"), VelopackPackFolder.Root);
        else
            Assert.Null(VelopackPackFolder.Root);
    }

    [Fact]
    public void Contains_LegacyManualLlamaServerPath_IsTrue()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // The exact value the old placeholder told users to type, and the value found
        // in a real settings.json in the wild — trailing separator included.
        Assert.True(VelopackPackFolder.Contains(
            Path.Combine(LocalAppData, "parlotype", "llama-server")));
        Assert.True(VelopackPackFolder.Contains(
            Path.Combine(LocalAppData, "parlotype", "llama-server") + Path.DirectorySeparatorChar));
    }

    [Fact]
    public void Contains_IsCaseInsensitive()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // Case-folding is the whole reason parlotype and Parlotype collide.
        Assert.True(VelopackPackFolder.Contains(Path.Combine(LocalAppData, "PARLOTYPE", "llama-server")));
        Assert.True(VelopackPackFolder.Contains(Path.Combine(LocalAppData, "Parlotype")));
    }

    [Fact]
    public void Contains_DataRoot_IsFalse()
    {
        if (!OperatingSystem.IsWindows())
            return;

        // parlotype-data shares the pack folder's first nine characters, so a prefix
        // test without a separator would wrongly flag every managed install.
        Assert.False(VelopackPackFolder.Contains(
            Path.Combine(LocalAppData, AppPaths.WindowsFolderName, "llama-server")));
        Assert.False(VelopackPackFolder.Contains(AppPaths.Default.LlamaServerInstallsDirectory));
    }

    [Fact]
    public void Contains_UnrelatedPath_IsFalse()
    {
        Assert.False(VelopackPackFolder.Contains(
            OperatingSystem.IsWindows() ? @"C:\tools\llama" : "/opt/llama"));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Contains_BlankInput_IsFalse(string? path)
    {
        Assert.False(VelopackPackFolder.Contains(path));
    }

    [Fact]
    public void Contains_MalformedPath_IsFalseRatherThanThrowing()
    {
        // Runs on every keystroke in the folder box, so half-typed nonsense must not
        // take the settings page down.
        Assert.False(VelopackPackFolder.Contains("\0"));
        Assert.False(VelopackPackFolder.Contains(new string('x', 8192)));
    }
}
