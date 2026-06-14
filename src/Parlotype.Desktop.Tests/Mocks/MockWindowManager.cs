using Parlotype.Desktop.Services;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Tests.Mocks;

public sealed class MockWindowManager : IWindowManager
{
    public int ShowTranscribeCount { get; private set; }
    public int ShowSettingsCount { get; private set; }
    public int HideTranscribeCount { get; private set; }
    public int ExitCount { get; private set; }

    /// <summary>The section requested by the most recent <see cref="ShowSettings"/> call.</summary>
    public SettingsSection? LastSettingsSection { get; private set; }

    public void ShowTranscribe(bool activate = true) => ShowTranscribeCount++;

    public void ShowSettings(SettingsSection? section = null)
    {
        ShowSettingsCount++;
        LastSettingsSection = section;
    }

    public void HideTranscribe() => HideTranscribeCount++;
    public void Exit() => ExitCount++;
}
