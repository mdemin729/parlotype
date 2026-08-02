using Parlotype.Core.Settings;
using Xunit;

namespace Parlotype.Tests;

public class AppPathsTests
{
    /// <summary>
    /// The pack id passed to <c>vpk pack</c>. Velopack installs to
    /// <c>%LOCALAPPDATA%\{packId}</c> and deletes that folder wholesale on
    /// uninstall — and on a re-run of Setup.exe.
    /// </summary>
    private const string VelopackPackId = "Parlotype";

    [Fact]
    public void DataDirectory_DoesNotCollideWithTheVelopackPackFolder()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var packFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            VelopackPackId);

        // Case-INSENSITIVE, deliberately: Windows treats %LOCALAPPDATA%\parlotype
        // and %LOCALAPPDATA%\Parlotype as the same directory, which is exactly how
        // the original layout would have had Velopack delete every downloaded
        // model and the user's API keys on uninstall (ADR-053).
        Assert.False(
            AppPaths.Default.DataDirectory.Equals(packFolder, StringComparison.OrdinalIgnoreCase),
            $"The data directory must not be the Velopack pack folder ({packFolder}) — "
            + "Velopack deletes that folder on uninstall.");
    }

    [Theory]
    [InlineData(nameof(IAppPaths.DataDirectory))]
    [InlineData(nameof(IAppPaths.SettingsDirectory))]
    [InlineData(nameof(IAppPaths.ModelsDirectory))]
    [InlineData(nameof(IAppPaths.LogsDirectory))]
    [InlineData(nameof(IAppPaths.LlamaServerDirectory))]
    [InlineData(nameof(IAppPaths.LlamaServerInstallsDirectory))]
    public void Directories_AreNotNestedInsideTheVelopackPackFolder(string propertyName)
    {
        if (!OperatingSystem.IsWindows())
            return;

        var packFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            VelopackPackId) + Path.DirectorySeparatorChar;

        var value = (string)typeof(IAppPaths).GetProperty(propertyName)!
            .GetValue(AppPaths.Default)!;

        Assert.False(
            value.StartsWith(packFolder, StringComparison.OrdinalIgnoreCase),
            $"{propertyName} ({value}) is inside the Velopack pack folder and would be "
            + "destroyed on update or uninstall.");
    }

    [Fact]
    public void WellKnownFiles_LiveUnderTheSettingsDirectory()
    {
        var paths = AppPaths.Default;

        Assert.Equal(paths.SettingsDirectory, Path.GetDirectoryName(paths.SettingsFilePath));
        Assert.Equal(paths.SettingsDirectory, Path.GetDirectoryName(paths.SecretsFilePath));
        Assert.Equal(paths.SettingsDirectory, Path.GetDirectoryName(paths.WindowStateFilePath));
    }

    [Fact]
    public void AllPaths_AreAbsolute()
    {
        var paths = AppPaths.Default;

        Assert.True(Path.IsPathRooted(paths.DataDirectory));
        Assert.True(Path.IsPathRooted(paths.SettingsDirectory));
        Assert.True(Path.IsPathRooted(paths.ModelsDirectory));
        Assert.True(Path.IsPathRooted(paths.LogsDirectory));
    }

    [Fact]
    public void ResolvingPaths_DoesNotCreateDirectories()
    {
        // Path resolution happens in constructors and static initialisers all over
        // the app; creating directories as a side effect would litter the disk of
        // anyone who merely opened Settings.
        var paths = new AppPaths();

        Assert.False(Directory.Exists(Path.Combine(paths.DataDirectory, "__probe_should_not_exist__")));
    }
}
