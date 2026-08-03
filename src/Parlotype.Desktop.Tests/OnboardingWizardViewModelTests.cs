using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Onboarding;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Wizard navigation state (ADR-055): Back/Next/Skip semantics, progress
/// bookkeeping, and the per-step window-manager side effects.
/// </summary>
public class OnboardingWizardViewModelTests
{
    private static (OnboardingWizardViewModel Vm, MockWindowManager Windows, MockGlobalHotkeyService Hotkeys) Build()
    {
        var windows = new MockWindowManager();
        var hotkeys = new MockGlobalHotkeyService();
        var vm = new OnboardingWizardViewModel(windows, hotkeys);
        return (vm, windows, hotkeys);
    }

    [Fact]
    public void Start_ResetsToFirstStep()
    {
        var (vm, _, _) = Build();

        vm.Start();

        Assert.Equal(8, vm.Steps.Count);
        Assert.Equal(0, vm.CurrentIndex);
        Assert.True(vm.IsFirstStep);
        Assert.False(vm.IsLastStep);
        Assert.Equal("welcome", vm.CurrentStep!.Id);
        Assert.True(vm.Steps[0].IsCurrent);
        Assert.Equal("Step 1 of 8", vm.ProgressText);
    }

    [Fact]
    public void WelcomeStep_OpensNoWindow()
    {
        var (vm, windows, _) = Build();

        vm.Start();

        Assert.Equal(0, windows.ShowTranscribeCount);
        Assert.Equal(0, windows.ShowSettingsCount);
    }

    [Fact]
    public void Next_AdvancesAndOpensTheStepsTargetWindow()
    {
        var (vm, windows, _) = Build();
        vm.Start();

        vm.NextCommand.Execute(null); // recording
        Assert.Equal(1, vm.CurrentIndex);
        Assert.Equal(1, windows.ShowTranscribeCount);
        Assert.True(vm.Steps[1].IsCurrent);
        Assert.False(vm.Steps[0].IsCurrent);

        vm.NextCommand.Execute(null); // widget
        Assert.Equal(2, windows.ShowTranscribeCount);

        vm.NextCommand.Execute(null); // engine
        Assert.Equal(1, windows.ShowSettingsCount);
        Assert.Equal(SettingsSection.Engine, windows.LastSettingsSection);

        vm.NextCommand.Execute(null); // model
        Assert.Equal(2, windows.ShowSettingsCount);
        Assert.Equal(SettingsSection.EngineModel, windows.LastSettingsSection);

        vm.NextCommand.Execute(null); // cloud
        Assert.Equal(3, windows.ShowSettingsCount);
        Assert.Equal(SettingsSection.Engine, windows.LastSettingsSection);

        vm.NextCommand.Execute(null); // tray
        Assert.Equal(3, windows.ShowTranscribeCount);
    }

    [Fact]
    public void Back_DecrementsButNeverLeavesTheFirstStep()
    {
        var (vm, _, _) = Build();
        vm.Start();

        vm.BackCommand.Execute(null);
        Assert.Equal(0, vm.CurrentIndex);

        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);
        vm.BackCommand.Execute(null);
        Assert.Equal(1, vm.CurrentIndex);
    }

    [Fact]
    public void LastStep_ShowsFinish_AndNextRaisesCloseRequested()
    {
        var (vm, _, _) = Build();
        vm.Start();
        var closeRequests = 0;
        vm.CloseRequested += (_, _) => closeRequests++;

        for (var i = 0; i < vm.Steps.Count - 1; i++)
            vm.NextCommand.Execute(null);

        Assert.True(vm.IsLastStep);
        Assert.Equal(Strings.Onboarding_Nav_Finish, vm.NextButtonText);
        Assert.Equal(0, closeRequests);

        vm.NextCommand.Execute(null);
        Assert.Equal(1, closeRequests);
        Assert.Equal(vm.Steps.Count - 1, vm.CurrentIndex);
    }

    [Fact]
    public void Skip_RaisesCloseRequested_FromAnyStep()
    {
        var (vm, _, _) = Build();
        vm.Start();
        var closeRequests = 0;
        vm.CloseRequested += (_, _) => closeRequests++;

        vm.NextCommand.Execute(null);
        vm.SkipCommand.Execute(null);

        Assert.Equal(1, closeRequests);
    }

    [Fact]
    public void Start_AfterProgress_ReturnsToFirstStep()
    {
        var (vm, _, _) = Build();
        vm.Start();
        vm.NextCommand.Execute(null);
        vm.NextCommand.Execute(null);

        vm.Start();

        Assert.Equal(0, vm.CurrentIndex);
        Assert.True(vm.Steps[0].IsCurrent);
    }

    [Fact]
    public void Start_RebuildsStepsFromCurrentBindings()
    {
        var (vm, _, hotkeys) = Build();
        vm.Start();
        var recording = vm.Steps.Single(s => s.Step.Id == "recording").Step;
        Assert.Equal("Hold Right Ctrl — Push to talk", recording.DetailLines[0]);

        // The user rebinds while the app runs; a re-launched tour must show
        // the new gesture, not the one captured at construction time.
        hotkeys.UpdateBindings([DictationHotkey.Hold(ModifierKey.Alt, ModifierSide.Right)]);
        vm.Start();

        recording = vm.Steps.Single(s => s.Step.Id == "recording").Step;
        Assert.Equal("Hold Right Alt — Push to talk", recording.DetailLines[0]);
    }

    [Fact]
    public void NullHotkeyService_YieldsFallbackHotkeyLine()
    {
        var vm = new OnboardingWizardViewModel(new MockWindowManager(), hotkeyService: null);

        vm.Start();

        var recording = vm.Steps.Single(s => s.Step.Id == "recording").Step;
        Assert.Equal([Strings.Onboarding_Hotkeys_None], recording.DetailLines);
    }
}
