namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// A Settings section that callers can deep-link to when opening the Settings
/// window (e.g. the Transcribe strip routing the user to the Language page).
/// </summary>
public enum SettingsSection
{
    /// <summary>The Language selection section.</summary>
    Language,

    /// <summary>
    /// The Cloud providers section (base URL / model / API key for the opt-in
    /// cloud engines, ADR-043). Only visible while a cloud engine is selected.
    /// </summary>
    CloudProviders,
}
