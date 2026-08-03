using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Onboarding;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Wizard window chrome and interaction (ADR-055): frameless Topmost card,
/// step content bound to the VM, Next/Back buttons drive the index, Esc and ✕
/// skip the tour.
/// </summary>
public class OnboardingWindowTests
{
    private static (OnboardingWindow Window, OnboardingWizardViewModel Vm) CreateWindow()
    {
        var vm = new OnboardingWizardViewModel(new MockWindowManager(), new MockGlobalHotkeyService());
        vm.Start();
        var window = new OnboardingWindow { DataContext = vm };
        return (window, vm);
    }

    /// <summary>
    /// Clicks through the real input pipeline — raising <c>Button.ClickEvent</c>
    /// directly would bypass <c>OnClick</c> and never invoke the bound command.
    /// </summary>
    private static void Click(Window window, Button button)
    {
        Dispatcher.UIThread.RunJobs();
        var point = button.TranslatePoint(
            new Point(button.Bounds.Width / 2, button.Bounds.Height / 2), window)!.Value;
        window.MouseDown(point, MouseButton.Left);
        window.MouseUp(point, MouseButton.Left);
        Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public void Window_IsFramelessTopmostCard()
    {
        var (window, _) = CreateWindow();

        Assert.Equal(WindowDecorations.None, window.WindowDecorations);
        Assert.True(window.Topmost);
        Assert.False(window.CanResize);
        Assert.Equal(380, window.Width);
        Assert.Equal(Strings.Onboarding_WindowTitle, window.Title);

        window.Close();
    }

    [AvaloniaFact]
    public void Window_ShowsFirstStepContent()
    {
        var (window, _) = CreateWindow();
        window.Show();

        Assert.Equal(Strings.Onboarding_Welcome_Title, window.FindControl<TextBlock>("StepTitle")!.Text);
        Assert.Equal(Strings.Onboarding_Welcome_Body, window.FindControl<TextBlock>("StepBody")!.Text);
        Assert.Equal("Step 1 of 8", window.FindControl<TextBlock>("ProgressLabel")!.Text);
        // The welcome step has no detail lines — the bullet list collapses.
        Assert.False(window.FindControl<ItemsControl>("DetailLines")!.IsVisible);

        window.Close();
    }

    [AvaloniaFact]
    public void NextButton_AdvancesStep_AndBackReturnsToIt()
    {
        var (window, vm) = CreateWindow();
        window.Show();

        var next = window.FindControl<Button>("NextButton")!;
        var back = window.FindControl<Button>("BackButton")!;
        Assert.False(back.IsEnabled);

        Click(window, next);
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(Strings.Onboarding_Recording_Title, window.FindControl<TextBlock>("StepTitle")!.Text);
        // The recording step lists the hotkeys.
        Assert.True(window.FindControl<ItemsControl>("DetailLines")!.IsVisible);
        Assert.True(back.IsEnabled);

        Click(window, back);
        Assert.Equal(0, vm.CurrentIndex);

        window.Close();
    }

    [AvaloniaFact]
    public void SkipButton_RaisesCloseRequested()
    {
        var (window, vm) = CreateWindow();
        window.Show();
        var closeRequests = 0;
        vm.CloseRequested += (_, _) => closeRequests++;

        Click(window, window.FindControl<Button>("SkipButton")!);

        Assert.Equal(1, closeRequests);
        window.Close();
    }

    [AvaloniaFact]
    public void CloseGlyph_RaisesCloseRequested()
    {
        var (window, vm) = CreateWindow();
        window.Show();
        var closeRequests = 0;
        vm.CloseRequested += (_, _) => closeRequests++;

        Click(window, window.FindControl<Button>("CloseButton")!);

        Assert.Equal(1, closeRequests);
        window.Close();
    }

    [AvaloniaFact]
    public void Escape_RaisesCloseRequested()
    {
        var (window, vm) = CreateWindow();
        window.Show();
        var closeRequests = 0;
        vm.CloseRequested += (_, _) => closeRequests++;

        window.KeyPressQwerty(PhysicalKey.Escape, RawInputModifiers.None);

        Assert.Equal(1, closeRequests);
        window.Close();
    }
}
