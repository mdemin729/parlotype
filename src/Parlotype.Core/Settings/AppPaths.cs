namespace Parlotype.Core.Settings;

/// <summary>
/// Platform-aware <see cref="IAppPaths"/> implementation.
/// </summary>
/// <remarks>
/// <para>
/// Lives in Core rather than Platform — against the usual "interfaces in Core,
/// implementations in Platform" rule — for two reasons: it depends on nothing but
/// the BCL, and <see cref="Speech.ParakeetModelInfo"/> and
/// <see cref="Speech.Gemma4ModelInfo"/> are Core types that must resolve the model
/// cache directory. Core cannot reference Platform, so a Platform-side
/// implementation would force those two back to hand-rolled path logic and
/// defeat the point of having one source of truth (ADR-053).
/// </para>
/// <para>
/// The Windows data root is <c>parlotype-data</c>, deliberately <em>not</em>
/// <c>parlotype</c>: Velopack installs to <c>%LOCALAPPDATA%\{packId}</c>, the packId
/// is <c>Parlotype</c>, and Windows paths are case-insensitive — so the old
/// <c>%LOCALAPPDATA%\parlotype</c> folder is the same directory as the Velopack pack
/// folder, which Velopack wipes on uninstall and on a re-run of Setup.exe.
/// </para>
/// </remarks>
public sealed class AppPaths : IAppPaths
{
    /// <summary>Windows data/settings root, under <c>%LOCALAPPDATA%</c>.</summary>
    public const string WindowsFolderName = "parlotype-data";

    /// <summary>macOS bundle-style folder name, and the Linux XDG subfolder name.</summary>
    private const string MacFolderName = "Parlotype";
    private const string XdgFolderName = "parlotype";

    /// <summary>Process-wide instance. Registered as the DI singleton for <see cref="IAppPaths"/>.</summary>
    public static AppPaths Default { get; } = new();

    public AppPaths()
    {
        if (OperatingSystem.IsWindows())
        {
            DataDirectory = Path.Combine(LocalAppData, WindowsFolderName);
            SettingsDirectory = DataDirectory;
            LogsDirectory = Path.Combine(DataDirectory, "logs");
        }
        else if (OperatingSystem.IsMacOS())
        {
            DataDirectory = Path.Combine(Home, "Library", "Application Support", MacFolderName);
            SettingsDirectory = DataDirectory;
            LogsDirectory = Path.Combine(Home, "Library", "Logs", MacFolderName);
        }
        else
        {
            DataDirectory = Path.Combine(
                XdgOrFallback("XDG_DATA_HOME", Path.Combine(".local", "share")), XdgFolderName);
            SettingsDirectory = Path.Combine(
                XdgOrFallback("XDG_CONFIG_HOME", ".config"), XdgFolderName);
            LogsDirectory = Path.Combine(
                XdgOrFallback("XDG_STATE_HOME", Path.Combine(".local", "state")), XdgFolderName, "logs");
        }

        ModelsDirectory = Path.Combine(DataDirectory, "models");
        LlamaServerDirectory = Path.Combine(DataDirectory, "llama-server");
        LlamaServerInstallsDirectory = Path.Combine(DataDirectory, "llama-servers");
        SettingsFilePath = Path.Combine(SettingsDirectory, "settings.json");
        SecretsFilePath = Path.Combine(SettingsDirectory, "secrets.json");
        WindowStateFilePath = Path.Combine(SettingsDirectory, "window-state.json");
    }

    public string DataDirectory { get; }
    public string SettingsDirectory { get; }
    public string ModelsDirectory { get; }
    public string LogsDirectory { get; }
    public string LlamaServerDirectory { get; }
    public string LlamaServerInstallsDirectory { get; }
    public string SettingsFilePath { get; }
    public string SecretsFilePath { get; }
    public string WindowStateFilePath { get; }

    private static string LocalAppData =>
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);

    private static string Home =>
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    /// <summary>
    /// Reads an XDG base-directory variable, falling back to its spec-defined
    /// default under the home directory. Relative values are ignored, as the
    /// spec requires them to be absolute.
    /// </summary>
    private static string XdgOrFallback(string variable, string fallbackRelativeToHome)
    {
        var value = Environment.GetEnvironmentVariable(variable);
        return !string.IsNullOrWhiteSpace(value) && Path.IsPathRooted(value)
            ? value
            : Path.Combine(Home, fallbackRelativeToHome);
    }
}
