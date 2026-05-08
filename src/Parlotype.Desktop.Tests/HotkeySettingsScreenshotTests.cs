using Avalonia.Headless.XUnit;
using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class HotkeyScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "hotkey-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Hotkey Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class HotkeySettingsScreenshotTests : IClassFixture<HotkeyScreenshotReportFixture>
{
    private readonly HotkeyScreenshotReportFixture _report;

    public HotkeySettingsScreenshotTests(HotkeyScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultHotkey()
    {
        var settings = new MockSettingsService();
        var hotkeyService = new MockGlobalHotkeyService();
        var vm = new HotkeySettingsViewModel(hotkeyService, settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new HotkeySettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default hotkey binding: Ctrl+Shift+Space. Activation mode is Push to Talk.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default Hotkey",
            "Fresh install defaults — Ctrl+Shift+Space with Push to Talk mode.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ToggleMode()
    {
        var settings = new MockSettingsService();
        var hotkeyService = new MockGlobalHotkeyService();
        var vm = new HotkeySettingsViewModel(hotkeyService, settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Default Push to Talk
        var view1 = new HotkeySettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Initial state: Push to Talk is selected.",
            screenshot1));

        // Step 2: Switch to Toggle
        vm.SetActivationMode(ActivationMode.Toggle);
        await SettleAsync();

        var view2 = new HotkeySettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User switches to Toggle mode. The Toggle radio button is now selected.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Switch to Toggle Mode",
            "User changes activation from Push to Talk to Toggle.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ConflictWarning()
    {
        var settings = new MockSettingsService();
        var hotkeyService = new MockGlobalHotkeyService();
        var vm = new HotkeySettingsViewModel(hotkeyService, settings);
        await SettleAsync();

        // Apply a binding that conflicts with a reserved shortcut (Win+L = Lock workstation)
        vm.ApplyRecordedBinding(new HotkeyBinding(HotkeyModifiers.Meta, "L"));
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new HotkeySettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "User sets Win+L as the hotkey. A conflict warning appears in orange, indicating this shortcut is reserved for \"Lock workstation\".",
            screenshot));

        _report.AddScenario(new Scenario(
            "Hotkey Conflict Warning",
            "User selects a key combination that conflicts with a reserved OS shortcut.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_RecordingState()
    {
        var settings = new MockSettingsService();
        var hotkeyService = new MockGlobalHotkeyService();
        var vm = new HotkeySettingsViewModel(hotkeyService, settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Normal state
        var view1 = new HotkeySettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Normal state showing current binding (Ctrl+Shift+Space).",
            screenshot1));

        // Step 2: Recording state
        vm.StartRecordingCommand.Execute(null);
        await SettleAsync();

        var view2 = new HotkeySettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "Recording state: the recorder button shows \"Press keys...\" prompting the user to press their desired key combination.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Hotkey Recording State",
            "User clicks the recorder button and enters the key capture state.",
            steps));
    }
}
