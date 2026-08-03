using Avalonia.Headless.XUnit;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Settings → Help (ADR-056): always-visible Application section, live hotkey
/// reference, and the tour re-launch command.
/// </summary>
public class HelpSettingsViewModelTests
{
    [Fact]
    public void TitleAndCategory_PlaceTheSectionUnderApplication()
    {
        var vm = new HelpSettingsViewModel(new MockOnboardingService());

        Assert.Equal(Strings.Help_Title, vm.Title);
        Assert.Equal(SettingsCategory.Application, vm.Category);
    }

    [Fact]
    public void Section_IsVisibleForEveryEngine()
    {
        var vm = new HelpSettingsViewModel(new MockOnboardingService());

        foreach (var engine in Enum.GetValues<SpeechEngine>())
            Assert.True(vm.IsVisibleFor(engine));
    }

    [Fact]
    public void HotkeyLines_ListBindingsWithModeLabels_AndEscLine()
    {
        var vm = new HelpSettingsViewModel(new MockOnboardingService(), new MockGlobalHotkeyService());

        Assert.Equal(
            [
                "Hold Right Ctrl — Push to talk",
                "Double-tap Ctrl — Toggle",
                "Ctrl+Alt+Space — Toggle",
                Strings.Help_EscCancelLine,
            ],
            vm.HotkeyLines);
    }

    [Fact]
    public void NullHotkeyService_ShowsFallbackLine()
    {
        var vm = new HelpSettingsViewModel(new MockOnboardingService(), hotkeyService: null);

        Assert.Equal([Strings.Help_NoHotkeys], vm.HotkeyLines);
    }

    [AvaloniaFact]
    public void BindingsChange_RefreshesTheLines()
    {
        var hotkeys = new MockGlobalHotkeyService();
        var vm = new HelpSettingsViewModel(new MockOnboardingService(), hotkeys);

        hotkeys.UpdateBindings([DictationHotkey.Hold(ModifierKey.Alt, ModifierSide.Right)]);

        Assert.Equal(
            ["Hold Right Alt — Push to talk", Strings.Help_EscCancelLine],
            vm.HotkeyLines);
    }

    [AvaloniaFact]
    public void RemovingEveryBinding_ShowsFallbackLine()
    {
        var hotkeys = new MockGlobalHotkeyService();
        var vm = new HelpSettingsViewModel(new MockOnboardingService(), hotkeys);

        hotkeys.UpdateBindings([]);

        Assert.Equal([Strings.Help_NoHotkeys], vm.HotkeyLines);
    }

    [Fact]
    public void OpenTourCommand_ShowsTheWizard()
    {
        var onboarding = new MockOnboardingService();
        var vm = new HelpSettingsViewModel(onboarding);

        vm.OpenTourCommand.Execute(null);

        Assert.Equal(1, onboarding.ShowWizardCount);
    }
}
