namespace Parlotype.Desktop.Services;

/// <summary>
/// Owns the onboarding tour's lifecycle (ADR-055): the once-only auto-show
/// after install and manual re-launches from Settings → Help.
/// </summary>
public interface IOnboardingService
{
    /// <summary>
    /// Shows the tour if it has never been offered
    /// (<see cref="Parlotype.Core.Settings.SettingsKeys.OnboardingCompleted"/>
    /// unset), recording the flag first so it can only ever fire once. Never
    /// throws — onboarding must not break app startup.
    /// </summary>
    Task MaybeShowOnFirstRunAsync();

    /// <summary>Opens (or re-activates) the tour window unconditionally.</summary>
    void ShowWizard();
}
