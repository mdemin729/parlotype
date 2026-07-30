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

    private static async Task<HotkeySettingsViewModel> CreateViewModelAsync()
    {
        var vm = new HotkeySettingsViewModel(new MockGlobalHotkeyService(), new MockSettingsService());
        await SettleAsync();
        return vm;
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultBindings()
    {
        var vm = await CreateViewModelAsync();

        var view = new HotkeySettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);

        _report.AddScenario(new Scenario(
            "Default Bindings",
            "What a fresh install ships with: hold Right Ctrl to talk, double-tap Ctrl for hands-free, and Ctrl+Alt+Space as an explicit chord.",
            [
                new ScenarioStep(
                    "Three bindings are listed, each with its activation mode. Only the chord's mode can be changed — a hold must be push-to-talk and a double-tap must toggle.",
                    screenshot)
            ]));
    }

    [AvaloniaFact]
    public async Task Scenario_AddPreset()
    {
        var vm = await CreateViewModelAsync();

        var steps = new List<ScenarioStep>();

        var before = new HotkeySettingsView();
        steps.Add(new ScenarioStep(
            "Starting from the default three bindings.",
            await ScreenshotHelper.CaptureBase64Async(before, vm)));

        vm.AddPresetCommand.Execute(DictationHotkey.Hold(ModifierKey.Alt, ModifierSide.Right));
        await SettleAsync();

        var after = new HotkeySettingsView();
        steps.Add(new ScenarioStep(
            "After picking \"Hold Right Alt\" from the Add menu — a fourth binding joins the list.",
            await ScreenshotHelper.CaptureBase64Async(after, vm)));

        _report.AddScenario(new Scenario(
            "Add a Preset Binding",
            "Bare-modifier gestures come from a preset menu, since a key-capture field cannot express \"hold this key\".",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ReservedShortcutRejected()
    {
        var vm = await CreateViewModelAsync();

        // Win+L locks the workstation — the binding must not be accepted.
        vm.ApplyRecordedChord(new HotkeyBinding(HotkeyModifiers.Meta, "L"));
        await SettleAsync();

        var view = new HotkeySettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);

        _report.AddScenario(new Scenario(
            "Reserved Shortcut Rejected",
            "Validation against shortcuts the OS has already claimed.",
            [
                new ScenarioStep(
                    "The user records Win+L. It is rejected in red and never joins the list, because Windows uses it to lock the workstation.",
                    screenshot)
            ]));
    }

    [AvaloniaFact]
    public async Task Scenario_AdvisoryWarning()
    {
        var vm = await CreateViewModelAsync();

        // Accepted, but it is parameter-hints in Visual Studio and VS Code.
        vm.ApplyRecordedChord(new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space"));
        await SettleAsync();

        var view = new HotkeySettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);

        _report.AddScenario(new Scenario(
            "Advisory Warning",
            "Combinations that work but commonly collide with other applications are flagged rather than blocked.",
            [
                new ScenarioStep(
                    "Ctrl+Shift+Space is added to the list, with an amber note that it shows parameter hints in Visual Studio and VS Code.",
                    screenshot)
            ]));
    }

    [AvaloniaFact]
    public async Task Scenario_DuplicateRejected()
    {
        var vm = await CreateViewModelAsync();

        // Ctrl+Alt+Space is already in the default set.
        vm.ApplyRecordedChord(new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space"));
        await SettleAsync();

        var view = new HotkeySettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);

        _report.AddScenario(new Scenario(
            "Duplicate Binding Rejected",
            "Validation against the user's own existing bindings, not just the OS.",
            [
                new ScenarioStep(
                    "Recording Ctrl+Alt+Space when it is already bound is refused, naming what already owns it.",
                    screenshot)
            ]));
    }

    [AvaloniaFact]
    public async Task Scenario_RecordingState()
    {
        var vm = await CreateViewModelAsync();

        var steps = new List<ScenarioStep>();

        var idle = new HotkeySettingsView();
        steps.Add(new ScenarioStep(
            "Idle: the chord recorder invites a key combination.",
            await ScreenshotHelper.CaptureBase64Async(idle, vm)));

        vm.StartRecordingCommand.Execute(null);
        await SettleAsync();

        var recording = new HotkeySettingsView();
        steps.Add(new ScenarioStep(
            "Armed: the button prompts for a key combination and the next keypress is captured as a chord.",
            await ScreenshotHelper.CaptureBase64Async(recording, vm)));

        _report.AddScenario(new Scenario(
            "Chord Recording State",
            "Capturing an explicit chord, which is still the only gesture a keyboard field can record directly.",
            steps));
    }
}
