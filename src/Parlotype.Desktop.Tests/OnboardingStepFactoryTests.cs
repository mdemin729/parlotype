using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.Onboarding;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.ViewModels;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The onboarding tour's step list (ADR-056): fixed order, complete copy, and
/// the recording step naming the user's actual hotkeys — including the case
/// where the user deliberately removed them all.
/// </summary>
public class OnboardingStepFactoryTests
{
    [Fact]
    public void Build_ProducesEightStepsInTourOrder()
    {
        var steps = OnboardingStepFactory.Build(DictationHotkeyDefaults.All);

        Assert.Equal(
            ["welcome", "recording", "widget", "engine", "model", "cloud", "tray", "recap"],
            steps.Select(s => s.Id));
    }

    [Fact]
    public void Build_EveryStep_HasTitleAndBody()
    {
        foreach (var step in OnboardingStepFactory.Build(DictationHotkeyDefaults.All))
        {
            Assert.False(string.IsNullOrWhiteSpace(step.Title), $"Step '{step.Id}' has no title");
            Assert.False(string.IsNullOrWhiteSpace(step.Body), $"Step '{step.Id}' has no body");
        }
    }

    [Fact]
    public void RecordingStep_ListsConfiguredBindings_WithModeLabels_AndEscLine()
    {
        var steps = OnboardingStepFactory.Build(DictationHotkeyDefaults.All);
        var recording = steps.Single(s => s.Id == "recording");

        // Shipped defaults: Hold Right Ctrl (PTT), Double-tap Ctrl (toggle),
        // Ctrl+Alt+Space (chord) — plus the Esc-cancel line.
        Assert.Equal(4, recording.DetailLines.Count);
        Assert.Equal("Hold Right Ctrl — Push to talk", recording.DetailLines[0]);
        Assert.Equal("Double-tap Ctrl — Toggle", recording.DetailLines[1]);
        Assert.Equal("Ctrl+Alt+Space — Toggle", recording.DetailLines[2]);
        Assert.Equal(Strings.Onboarding_Recording_EscLine, recording.DetailLines[3]);
    }

    [Fact]
    public void RecordingStep_WithNoBindings_ShowsFallbackLine()
    {
        // An empty stored list is a deliberate user choice (ADR-047) — the
        // tour must not resurrect defaults it does not have.
        var steps = OnboardingStepFactory.Build([]);
        var recording = steps.Single(s => s.Id == "recording");

        Assert.Equal([Strings.Onboarding_Hotkeys_None], recording.DetailLines);
    }

    [Fact]
    public void RecordingStep_WithNullBindings_ShowsFallbackLine()
    {
        var steps = OnboardingStepFactory.Build(null);
        var recording = steps.Single(s => s.Id == "recording");

        Assert.Equal([Strings.Onboarding_Hotkeys_None], recording.DetailLines);
    }

    [Fact]
    public void Steps_TargetTheExpectedWindowsAndElements()
    {
        var steps = OnboardingStepFactory.Build(DictationHotkeyDefaults.All);
        var byId = steps.ToDictionary(s => s.Id);

        Assert.Equal(OnboardingTargetWindow.None, byId["welcome"].TargetWindow);

        Assert.Equal(OnboardingTargetWindow.Transcribe, byId["recording"].TargetWindow);
        Assert.Equal([OnboardingTargetIds.TranscribeRecord], byId["recording"].TargetIds);

        Assert.Equal(OnboardingTargetWindow.Transcribe, byId["widget"].TargetWindow);
        Assert.Contains(OnboardingTargetIds.TranscribeGrip, byId["widget"].TargetIds);
        Assert.Contains(OnboardingTargetIds.TranscribeClose, byId["widget"].TargetIds);
        Assert.Contains(OnboardingTargetIds.TranscribeLanguageStrip, byId["widget"].TargetIds);

        Assert.Equal(OnboardingTargetWindow.Settings, byId["engine"].TargetWindow);
        Assert.Equal(SettingsSection.Engine, byId["engine"].SettingsSection);
        Assert.Equal([OnboardingTargetIds.SettingsEngineList], byId["engine"].TargetIds);

        Assert.Equal(OnboardingTargetWindow.Settings, byId["model"].TargetWindow);
        Assert.Equal(SettingsSection.EngineModel, byId["model"].SettingsSection);
        Assert.Equal([OnboardingTargetIds.SettingsModelList], byId["model"].TargetIds);

        // The Cloud providers section is unreachable under local engines, so
        // the cloud step points at the two cloud cards on the Engine page.
        Assert.Equal(OnboardingTargetWindow.Settings, byId["cloud"].TargetWindow);
        Assert.Equal(SettingsSection.Engine, byId["cloud"].SettingsSection);
        Assert.Equal(
            ["Settings.EngineCard.OpenAiCompatible", "Settings.EngineCard.XaiGrok"],
            byId["cloud"].TargetIds);

        Assert.Equal(OnboardingTargetWindow.Transcribe, byId["tray"].TargetWindow);
        Assert.Equal([OnboardingTargetIds.TranscribeClose], byId["tray"].TargetIds);

        Assert.Equal(OnboardingTargetWindow.None, byId["recap"].TargetWindow);
    }

    [Fact]
    public void RecapStep_RestatesThePrimaryGesture()
    {
        var steps = OnboardingStepFactory.Build(DictationHotkeyDefaults.All);
        var recap = steps.Single(s => s.Id == "recap");

        Assert.Equal([HotkeyHint.Describe(DictationHotkeyDefaults.All)], recap.DetailLines);
    }
}
