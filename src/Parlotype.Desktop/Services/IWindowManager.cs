namespace Parlotype.Desktop.Services;

using Parlotype.Desktop.ViewModels;

/// <summary>
/// Coordinates the lifetime of the V2 desktop's top-level windows.
/// Owns single-instance creation of the Transcribe and Settings windows
/// and routes tray + hotkey actions to them.
/// </summary>
public interface IWindowManager
{
    /// <summary>Show the Transcribe window. Creates it if needed.</summary>
    /// <param name="activate">When true, activates (focuses) the window; when false, shows without stealing focus.</param>
    void ShowTranscribe(bool activate = true);

    /// <summary>Show + activate the Settings window. Creates it if needed.</summary>
    /// <param name="section">When set, deep-links the window to that section; otherwise the last-viewed section is shown.</param>
    void ShowSettings(SettingsSection? section = null);

    /// <summary>Hide the Transcribe window if it is open.</summary>
    void HideTranscribe();

    /// <summary>Shut the application down explicitly.</summary>
    void Exit();
}
