using Parlotype.Core.Settings;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// <see cref="IAppPaths"/> rooted at a throwaway temp directory, so tests that
/// exercise destructive paths can never reach the real user data folder.
/// </summary>
public sealed class MockAppPaths : IAppPaths, IDisposable
{
    public MockAppPaths()
    {
        DataDirectory = Path.Combine(Path.GetTempPath(), "parlotype-tests", Guid.NewGuid().ToString("N"));
        SettingsDirectory = DataDirectory;
        ModelsDirectory = Path.Combine(DataDirectory, "models");
        LogsDirectory = Path.Combine(DataDirectory, "logs");
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

    /// <summary>Creates <see cref="ModelsDirectory"/> with a dummy model file in it.</summary>
    public string WriteFakeModel(string fileName = "model.bin", int bytes = 2048)
    {
        Directory.CreateDirectory(ModelsDirectory);
        var path = Path.Combine(ModelsDirectory, fileName);
        File.WriteAllBytes(path, new byte[bytes]);
        return path;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(DataDirectory))
                Directory.Delete(DataDirectory, recursive: true);
        }
        catch
        {
            // Temp cleanup is best-effort; never fail a test over it.
        }
    }
}
