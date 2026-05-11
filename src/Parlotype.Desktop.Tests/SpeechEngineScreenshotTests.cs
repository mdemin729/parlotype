using Avalonia.Headless.XUnit;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class SpeechEngineScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "speech-engine-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Speech Engine Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class SpeechEngineScreenshotTests : IClassFixture<SpeechEngineScreenshotReportFixture>
{
    private readonly SpeechEngineScreenshotReportFixture _report;

    public SpeechEngineScreenshotTests(SpeechEngineScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultWhisperSelected()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new SpeechEngineSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default state: Whisper is selected as the speech engine. " +
            "Two options are shown — Whisper (default, well-tested) and Gemma 4 (Experimental).",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default — Whisper Selected",
            "Fresh install. Whisper is the default speech engine.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_SwitchToGemma4()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Default state
        var view1 = new SpeechEngineSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Initial state: Whisper is selected.",
            screenshot1));

        // Step 2: Select Gemma 4
        vm.SelectEngineCommand.Execute(SpeechEngine.Gemma4);
        await SettleAsync();

        var view2 = new SpeechEngineSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects Gemma 4 (Experimental). The selection indicator moves to Gemma 4. " +
            "An information panel appears explaining that Gemma 4 E4B runs via a local llama-server process, " +
            "supports English only, works best with clean speech, and requires a ~10 GB download.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Switch to Gemma 4",
            "User switches from Whisper to Gemma 4. The experimental info panel is displayed.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_SwitchBackToWhisper()
    {
        var settings = new MockSettingsService();
        var vm = new SpeechEngineSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Select Gemma 4
        vm.SelectEngineCommand.Execute(SpeechEngine.Gemma4);
        await SettleAsync();

        var view1 = new SpeechEngineSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Gemma 4 is selected. The experimental info panel is visible.",
            screenshot1));

        // Step 2: Switch back to Whisper
        vm.SelectEngineCommand.Execute(SpeechEngine.Whisper);
        await SettleAsync();

        var view2 = new SpeechEngineSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User switches back to Whisper. The Gemma 4 info panel disappears. " +
            "Whisper is once again the selected engine.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Switch Back to Whisper",
            "User tries Gemma 4, then switches back to Whisper.",
            steps));
    }
}
