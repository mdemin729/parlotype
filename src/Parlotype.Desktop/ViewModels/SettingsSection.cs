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

    /// <summary>The Engine selection section (ADR-056, onboarding tour).</summary>
    Engine,

    /// <summary>
    /// The model page of whichever engine is currently active — Parakeet,
    /// Whisper or Gemma 4 (ADR-056). Falls back to the Engine section for
    /// cloud engines, which have no local model page.
    /// </summary>
    EngineModel,

    /// <summary>The Help section (hotkey reference + onboarding tour, ADR-056).</summary>
    Help,
}
