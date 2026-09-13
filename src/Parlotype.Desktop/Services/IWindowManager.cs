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

    /// <summary>
    /// Show the Transcribe window as the transient HUD for a dictation
    /// gesture: without focus, and set to hide itself once the session
    /// settles (ADR-068). A window the user already opened keeps its own
    /// lifetime — a held key cannot take away a window the user is using. A
    /// separate method from <see cref="ShowTranscribe"/> rather than a
    /// parameter on it: the two are different operations with different
    /// callers, and this leaves every existing call site untouched.
    /// </summary>
    void ShowTranscribeForDictation();

    /// <summary>Show + activate the Settings window. Creates it if needed.</summary>
    /// <param name="section">When set, deep-links the window to that section; otherwise the last-viewed section is shown.</param>
    void ShowSettings(SettingsSection? section = null);

    /// <summary>Hide the Transcribe window if it is open.</summary>
    void HideTranscribe();

    /// <summary>Shut the application down explicitly.</summary>
    void Exit();
}
