using Avalonia.Headless.XUnit;
using Parlotype.Core.Settings;
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
    public async Task Scenario_PunctuationAndProfanityOn()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.AutomaticPunctuationEnabled = true;
        vm.FilterProfanityEnabled = true;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new WhisperOutputSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Whisper output options enabled: punctuation on, profanity filter on. " +
            "(Translation lives on the Language settings page from now on.)",
            screenshot));

        _report.AddScenario(new Scenario(
            "Whisper Output — Toggles Enabled",
            "User enables punctuation and profanity filter.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_PunctuationAndProfanityOff()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.AutomaticPunctuationEnabled = false;
        vm.FilterProfanityEnabled = false;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new WhisperOutputSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Whisper output options disabled: punctuation stripped, profanity filter off.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Whisper Output — Toggles Disabled",
            "User disables punctuation and profanity filter.",
            steps));
    }
}

public class LanguageSettingsScreenshotTests : IClassFixture<SpeechScreenshotReportFixture>
{
    private readonly SpeechScreenshotReportFixture _report;

    public LanguageSettingsScreenshotTests(SpeechScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_TranslationDisabled_TargetGreyed()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(),
            TestContext.Current.CancellationToken);
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();
        var view = new LanguageSelectionSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default state: translation off, arrow dimmed, target button disabled.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Language — Translation Disabled",
            "Fresh install: source = Auto-detect, target button greyed, arrow off.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_TranslationEnabled_Whisper_TargetIsEnglish()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(),
            TestContext.Current.CancellationToken);
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await SettleAsync();

        vm.ToggleTranslationCommand.Execute(null);
        await SettleAsync();

        var steps = new List<ScenarioStep>();
        var view = new LanguageSelectionSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "User toggles translation on. Arrow lights up; target button becomes \"English\" (Whisper's only target).",
            screenshot));

        _report.AddScenario(new Scenario(
            "Language — Whisper Translation On",
            "Whisper engine: translation enabled, target = English.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_TranslationEnabled_Gemma4_ArbitraryTarget()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(),
            TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "ru",
            TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString(),
            TestContext.Current.CancellationToken);
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();
        var view = new LanguageSelectionSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Gemma 4 engine with translation on. Target is an arbitrary language (Russian) from the full catalog.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Language — Gemma 4 Translation On",
            "Gemma 4 engine: translation enabled, target = Russian.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_SourcePickerOpen_ShowsList()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(),
            TestContext.Current.CancellationToken);
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await SettleAsync();

        vm.OpenSourcePickerCommand.Execute(null);
        await SettleAsync();

        var steps = new List<ScenarioStep>();
        var view = new LanguageSelectionSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "User clicks the source language button. The picker expands inline; the source button gets the green focus border.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Language — Source Picker Open",
            "Clicking the source button reveals the picker (search + scrollable list).",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_TranslationPaused_OnNonTranslatingWhisperModel()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(),
            TestContext.Current.CancellationToken);
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await SettleAsync();

        vm.ToggleTranslationCommand.Execute(null);
        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);
        await SettleAsync();

        var steps = new List<ScenarioStep>();
        var view = new LanguageSelectionSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Translation is on but the selected Whisper model (Large v3 Turbo) can't translate. " +
            "Buttons stay enabled (preference preserved); the accent note explains why nothing will be translated.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Language — Translation Paused (accent note)",
            "Large v3 Turbo + translation on: accent note visible.",
            steps));
    }
}
