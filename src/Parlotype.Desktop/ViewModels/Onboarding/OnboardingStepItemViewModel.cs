using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Desktop.Onboarding;

namespace Parlotype.Desktop.ViewModels.Onboarding;

/// <summary>
/// One step wrapped for the wizard view (ADR-055): the immutable step data
/// plus the mutable "is this the current step" flag driving the progress dots.
/// </summary>
public sealed partial class OnboardingStepItemViewModel(OnboardingStep step) : ObservableObject
{
    public OnboardingStep Step { get; } = step;

    [ObservableProperty]
    private bool _isCurrent;
}
