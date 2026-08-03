using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Onboarding;

/// <summary>
/// One page of the onboarding tour (ADR-055). Declarative: the wizard
/// view-model opens <see cref="TargetWindow"/> (at
/// <see cref="SettingsSection"/> when it is the Settings window) and the
/// wizard window highlights <see cref="TargetIds"/> in it.
/// </summary>
/// <param name="Id">Stable step id, for tests and logging.</param>
/// <param name="Title">Localized step heading.</param>
/// <param name="Body">Localized step body text.</param>
/// <param name="TargetWindow">The live window this step opens, if any.</param>
/// <param name="SettingsSection">Deep-link section for Settings steps.</param>
/// <param name="TargetIds"><see cref="OnboardingTargetIds"/> to highlight.</param>
/// <param name="DetailLines">
/// Extra bullet lines under the body — dynamic content such as the user's
/// actual hotkey bindings.
/// </param>
public sealed record OnboardingStep(
    string Id,
    string Title,
    string Body,
    OnboardingTargetWindow TargetWindow,
    SettingsSection? SettingsSection,
    IReadOnlyList<string> TargetIds,
    IReadOnlyList<string> DetailLines);
