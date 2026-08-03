namespace Parlotype.Desktop.Onboarding;

/// <summary>Which live app window an onboarding step opens and points at (ADR-055).</summary>
public enum OnboardingTargetWindow
{
    /// <summary>No window — the step is text-only (welcome, recap).</summary>
    None,

    /// <summary>The Transcribe widget.</summary>
    Transcribe,

    /// <summary>The Settings window, at the step's <c>SettingsSection</c>.</summary>
    Settings,
}
