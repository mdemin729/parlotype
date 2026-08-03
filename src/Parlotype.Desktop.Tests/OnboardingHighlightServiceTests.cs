using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Parlotype.Desktop.Onboarding;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The onboarding highlight mechanism (ADR-055): controls marked with
/// <see cref="OnboardingTarget.IdProperty"/> get an
/// <see cref="OnboardingHighlight"/> adorner via the window's adorner layer;
/// unknown ids never throw; invisible targets are picked up later by the
/// layout-updated retry.
/// </summary>
public class OnboardingHighlightServiceTests
{
    private static (Window Window, Button Target) CreateWindowWithTarget(string id)
    {
        var target = new Button { Content = "target" };
        OnboardingTarget.SetId(target, id);
        var window = new Window
        {
            Width = 300,
            Height = 200,
            Content = new StackPanel { Children = { target } },
        };
        return (window, target);
    }

    [AvaloniaFact]
    public void Apply_AttachesAdorner_ToMarkedControl()
    {
        var (window, target) = CreateWindowWithTarget("Test.Target");
        window.Show();

        var service = new OnboardingHighlightService();
        service.Apply(window, ["Test.Target"]);

        Assert.IsType<OnboardingHighlight>(AdornerLayer.GetAdorner(target));

        service.Clear();
        window.Close();
    }

    [AvaloniaFact]
    public void Clear_DetachesAdorner()
    {
        var (window, target) = CreateWindowWithTarget("Test.Target");
        window.Show();

        var service = new OnboardingHighlightService();
        service.Apply(window, ["Test.Target"]);
        service.Clear();

        Assert.Null(AdornerLayer.GetAdorner(target));

        window.Close();
    }

    [AvaloniaFact]
    public void Apply_UnknownId_DoesNotThrow()
    {
        var (window, target) = CreateWindowWithTarget("Test.Target");
        window.Show();

        var service = new OnboardingHighlightService();
        service.Apply(window, ["No.Such.Id"]);

        Assert.Null(AdornerLayer.GetAdorner(target));

        service.Clear();
        window.Close();
    }

    [AvaloniaFact]
    public void Apply_InvisibleTarget_IsHighlightedOnceVisible()
    {
        var (window, target) = CreateWindowWithTarget("Test.Target");
        target.IsVisible = false;
        window.Show();

        var service = new OnboardingHighlightService();
        service.Apply(window, ["Test.Target"]);
        Assert.Null(AdornerLayer.GetAdorner(target));

        // Becoming visible triggers a layout pass; the pending-retry hook
        // should resolve the target then.
        target.IsVisible = true;
        window.UpdateLayout();
        Dispatcher.UIThread.RunJobs();

        Assert.IsType<OnboardingHighlight>(AdornerLayer.GetAdorner(target));

        service.Clear();
        window.Close();
    }

    [AvaloniaFact]
    public void Apply_SecondCall_SupersedesFirst()
    {
        var target1 = new Button { Content = "one" };
        var target2 = new Button { Content = "two" };
        OnboardingTarget.SetId(target1, "Test.One");
        OnboardingTarget.SetId(target2, "Test.Two");
        var window = new Window
        {
            Width = 300,
            Height = 200,
            Content = new StackPanel { Children = { target1, target2 } },
        };
        window.Show();

        var service = new OnboardingHighlightService();
        service.Apply(window, ["Test.One"]);
        service.Apply(window, ["Test.Two"]);

        Assert.Null(AdornerLayer.GetAdorner(target1));
        Assert.IsType<OnboardingHighlight>(AdornerLayer.GetAdorner(target2));

        service.Clear();
        window.Close();
    }
}
