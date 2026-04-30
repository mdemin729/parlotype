using Parlotype.Desktop.V2.Services;

namespace Parlotype.Desktop.V2.Tests.Mocks;

public sealed class MockWindowManager : IWindowManager
{
    public int ShowTranscribeCount { get; private set; }
    public int ShowSettingsCount { get; private set; }
    public int HideTranscribeCount { get; private set; }
    public int ExitCount { get; private set; }

    public void ShowTranscribe() => ShowTranscribeCount++;
    public void ShowSettings() => ShowSettingsCount++;
    public void HideTranscribe() => HideTranscribeCount++;
    public void Exit() => ExitCount++;
}
