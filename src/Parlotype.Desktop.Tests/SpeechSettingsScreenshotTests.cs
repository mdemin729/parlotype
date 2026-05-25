using Avalonia.Headless.XUnit;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class SpeechScreenshotReportFixture : IAsyncLifetime
{
    private readonly List<Scenario> _scenarios = [];

    public void AddScenario(Scenario scenario)
    {
        lock (_scenarios)
            _scenarios.Add(scenario);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        List<Scenario> snapshot;
        lock (_scenarios)
            snapshot = [.. _scenarios];

        if (snapshot.Count > 0)
        {
            var reportPath = Path.Combine(
                ScreenshotReportFixture.FindRepoRoot(), "reports", "speech-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Speech Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class SilenceTimeoutSettingsScreenshotTests : IClassFixture<SpeechScreenshotReportFixture>
{
    private readonly SpeechScreenshotReportFixture _report;

    public SilenceTimeoutSettingsScreenshotTests(SpeechScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultSilenceTimeout()
    {
        var settings = new MockSettingsService();
        var vm = new SilenceTimeoutSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new SilenceTimeoutSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default silence timeout: Medium selected.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default Silence Timeout",
            "Fresh install default — Medium wait time.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ChangeWaitTime()
    {
        var settings = new MockSettingsService();
        var vm = new SilenceTimeoutSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view1 = new SilenceTimeoutSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Initial state: Medium silence timeout is selected (default).",
            screenshot1));

        vm.SelectWaitTimeCommand.Execute(WaitTimeOption.Long);
        await SettleAsync();

        var view2 = new SilenceTimeoutSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects Long silence timeout. The selection indicator moves to \"Long\".",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Change Silence Timeout",
            "User changes the silence timeout from Medium to Long.",
            steps));
    }
}

public class WhisperOutputSettingsScreenshotTests : IClassFixture<SpeechScreenshotReportFixture>
{
    private readonly SpeechScreenshotReportFixture _report;

    public WhisperOutputSettingsScreenshotTests(SpeechScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_AllTogglesEnabled()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.AutomaticPunctuationEnabled = true;
        vm.FilterProfanityEnabled = true;
        vm.TranslateToEnglishEnabled = true;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new WhisperOutputSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "All Whisper output options enabled: punctuation on, profanity filter on, translation on.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Whisper Output — All Toggles Enabled",
            "User enables punctuation, profanity filter, and translation.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_AllTogglesDisabled()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.AutomaticPunctuationEnabled = false;
        vm.FilterProfanityEnabled = false;
        vm.TranslateToEnglishEnabled = false;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new WhisperOutputSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "All Whisper output options disabled: punctuation stripped, profanity filter off, translation off.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Whisper Output — All Toggles Disabled",
            "User disables punctuation, profanity filter, and translation.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_TranslationPaused_AccentNoteVisible()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);
        vm.TranslateToEnglishEnabled = true;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new WhisperOutputSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Model changed to Large v3 Turbo (no translation support). The translate toggle is disabled but checked — " +
            "the user's preference is preserved. The blue accent note reads: \"Translation is paused…\"",
            screenshot));

        _report.AddScenario(new Scenario(
            "Whisper Output — Translation Paused (accent note)",
            "Large v3 Turbo selected while translation preference is on: toggle greyed+checked, accent note visible.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_TranslationUnavailable_MutedNoteVisible()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);
        vm.TranslateToEnglishEnabled = false;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new WhisperOutputSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Model is Large v3 Turbo and translation preference is off. The muted note reads: " +
            "\"The selected model doesn't support translation.\"",
            screenshot));

        _report.AddScenario(new Scenario(
            "Whisper Output — Translation Unavailable (muted note)",
            "Large v3 Turbo selected while translation preference is off: toggle greyed+unchecked, muted note visible.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_TranslationRestored_NoteDisappears()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);
        vm.TranslateToEnglishEnabled = true;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view1 = new WhisperOutputSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Large v3 Turbo selected — translation paused, accent note visible.",
            screenshot1));

        vm.UpdateTranslationAvailability(WhisperModelType.Medium);
        await SettleAsync();

        var view2 = new WhisperOutputSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "Switched to Medium — translation capability restored. The accent note disappears and " +
            "the toggle re-enables with the user's preference (on) intact.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Whisper Output — Translation Restored",
            "Switching from Large v3 Turbo back to Medium restores the toggle and removes the note.",
            steps));
    }
}
