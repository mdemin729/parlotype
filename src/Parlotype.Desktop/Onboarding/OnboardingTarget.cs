using Avalonia;
using Avalonia.Controls;

namespace Parlotype.Desktop.Onboarding;

/// <summary>
/// Attached property that marks a control as a highlightable onboarding target
/// (ADR-056). Views tag elements with
/// <c>onb:OnboardingTarget.Id="{x:Static onb:OnboardingTargetIds...}"</c> and
/// <see cref="OnboardingHighlightService"/> finds them by scanning the visual
/// tree, so the wizard never holds references into view internals.
/// </summary>
public sealed class OnboardingTarget : AvaloniaObject
{
    public static readonly AttachedProperty<string?> IdProperty =
        AvaloniaProperty.RegisterAttached<OnboardingTarget, Control, string?>("Id");

    private OnboardingTarget()
    {
    }

    public static string? GetId(Control control) => control.GetValue(IdProperty);

    public static void SetId(Control control, string? value) => control.SetValue(IdProperty, value);
}
