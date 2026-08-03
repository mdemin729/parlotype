using Parlotype.Core.Hotkeys;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Onboarding;

/// <summary>
/// Builds the onboarding tour's step list (ADR-055). Rebuilt on every tour
/// launch so the recording step always names the user's <em>current</em>
/// hotkey bindings, not the ones from when the app started.
/// </summary>
public static class OnboardingStepFactory
{
    public static IReadOnlyList<OnboardingStep> Build(IReadOnlyList<DictationHotkey>? bindings)
    {
        var validBindings = bindings?.Where(b => b.IsValid).ToList() ?? [];

        // The user may have deliberately removed every hotkey — honour that
        // (HotkeySettingsMigrator keeps an empty stored list) and fall back to
        // pointing at the widget button instead of inventing defaults.
        List<string> hotkeyLines = validBindings.Count > 0
            ? [.. validBindings.Select(b => $"{b.DisplayString} — {b.ModeLabel}"), Strings.Onboarding_Recording_EscLine]
            : [Strings.Onboarding_Hotkeys_None];

        return
        [
            new OnboardingStep(
                "welcome",
                Strings.Onboarding_Welcome_Title,
                Strings.Onboarding_Welcome_Body,
                OnboardingTargetWindow.None,
                SettingsSection: null,
                TargetIds: [],
                DetailLines: []),

            new OnboardingStep(
                "recording",
                Strings.Onboarding_Recording_Title,
                Strings.Onboarding_Recording_Body,
                OnboardingTargetWindow.Transcribe,
                SettingsSection: null,
                TargetIds: [OnboardingTargetIds.TranscribeRecord],
                DetailLines: hotkeyLines),

            new OnboardingStep(
                "widget",
                Strings.Onboarding_Widget_Title,
                Strings.Onboarding_Widget_Body,
                OnboardingTargetWindow.Transcribe,
                SettingsSection: null,
                TargetIds:
                [
                    OnboardingTargetIds.TranscribeGrip,
                    OnboardingTargetIds.TranscribeClose,
                    OnboardingTargetIds.TranscribeLanguageStrip,
                ],
                DetailLines: []),

            new OnboardingStep(
                "engine",
                Strings.Onboarding_Engine_Title,
                Strings.Onboarding_Engine_Body,
                OnboardingTargetWindow.Settings,
                SettingsSection.Engine,
                TargetIds: [OnboardingTargetIds.SettingsEngineList],
                DetailLines: []),

            new OnboardingStep(
                "model",
                Strings.Onboarding_Model_Title,
                Strings.Onboarding_Model_Body,
                OnboardingTargetWindow.Settings,
                SettingsSection.EngineModel,
                TargetIds: [OnboardingTargetIds.SettingsModelList],
                DetailLines: []),

            new OnboardingStep(
                "cloud",
                Strings.Onboarding_Cloud_Title,
                Strings.Onboarding_Cloud_Body,
                OnboardingTargetWindow.Settings,
                SettingsSection.Engine,
                TargetIds:
                [
                    OnboardingTargetIds.EngineCard(SpeechEngine.OpenAiCompatible),
                    OnboardingTargetIds.EngineCard(SpeechEngine.XaiGrok),
                ],
                DetailLines: []),

            new OnboardingStep(
                "tray",
                Strings.Onboarding_Tray_Title,
                Strings.Onboarding_Tray_Body,
                OnboardingTargetWindow.Transcribe,
                SettingsSection: null,
                TargetIds: [OnboardingTargetIds.TranscribeClose],
                DetailLines: []),

            new OnboardingStep(
                "recap",
                Strings.Onboarding_Recap_Title,
                Strings.Onboarding_Recap_Body,
                OnboardingTargetWindow.None,
                SettingsSection: null,
                TargetIds: [],
                DetailLines: [HotkeyHint.Describe(validBindings)]),
        ];
    }
}
