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

public class SpeechSettingsScreenshotTests : IClassFixture<SpeechScreenshotReportFixture>
{
    private readonly SpeechScreenshotReportFixture _report;

    public SpeechSettingsScreenshotTests(SpeechScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultSpeechSettings()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new SpeechSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default speech settings: Medium silence timeout selected, automatic punctuation enabled, profanity filter off, translation off.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default Speech Settings",
            "Fresh install defaults — Medium wait time, punctuation on, profanity filter off, translation off.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ChangeWaitTime()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Default (Medium)
        var view1 = new SpeechSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Initial state: Medium silence timeout is selected (default).",
            screenshot1));

        // Step 2: Select Long
        vm.SelectWaitTimeCommand.Execute(WaitTimeOption.Long);
        await SettleAsync();

        var view2 = new SpeechSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects Long silence timeout. The selection indicator moves to \"Long\".",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Change Silence Timeout",
            "User changes the silence timeout from Medium to Long.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_AllTogglesEnabled()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechSettingsViewModel(settings);
        await SettleAsync();

        // Enable all toggles
        vm.AutomaticPunctuationEnabled = true;
        vm.FilterProfanityEnabled = true;
        vm.TranslateToEnglishEnabled = true;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new SpeechSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "All speech options enabled: punctuation on, profanity filter on (shows \"Profanity masked with ****\"), translation on (shows \"Translating to English\").",
            screenshot));

        _report.AddScenario(new Scenario(
            "All Toggles Enabled",
            "User enables all three speech toggles — punctuation, profanity filter, and translation.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_AllTogglesDisabled()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechSettingsViewModel(settings);
        await SettleAsync();

        // Disable all toggles
        vm.AutomaticPunctuationEnabled = false;
        vm.FilterProfanityEnabled = false;
        vm.TranslateToEnglishEnabled = false;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new SpeechSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "All speech options disabled: punctuation stripped, profanity filter off, translation off.",
            screenshot));

        _report.AddScenario(new Scenario(
            "All Toggles Disabled",
            "User disables all three speech toggles.",
            steps));
    }
}
