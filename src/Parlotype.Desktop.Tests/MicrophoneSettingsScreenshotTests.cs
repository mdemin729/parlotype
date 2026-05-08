using Avalonia.Headless.XUnit;
using Parlotype.Core.Audio;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class MicrophoneScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "microphone-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Microphone Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class MicrophoneSettingsScreenshotTests : IClassFixture<MicrophoneScreenshotReportFixture>
{
    private readonly MicrophoneScreenshotReportFixture _report;

    public MicrophoneSettingsScreenshotTests(MicrophoneScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_SingleMicrophone()
    {
        var mic = new MicrophoneInfo("mic-1", "Built-in Microphone", IsDefault: true);
        var enumerator = new MockMicrophoneEnumerator(mic);
        var settings = new MockSettingsService();
        var vm = new MicrophoneSettingsViewModel(enumerator, settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new MicrophoneSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Single microphone detected. The built-in microphone is selected by default and marked as \"System default\".",
            screenshot));

        _report.AddScenario(new Scenario(
            "Single Microphone",
            "User has one microphone. It is automatically selected.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_MultipleMicrophones()
    {
        var mic1 = new MicrophoneInfo("mic-1", "Built-in Microphone", IsDefault: true);
        var mic2 = new MicrophoneInfo("mic-2", "USB Condenser Mic", IsDefault: false);
        var mic3 = new MicrophoneInfo("mic-3", "Bluetooth Headset", IsDefault: false);
        var enumerator = new MockMicrophoneEnumerator(mic1, mic2, mic3);
        var settings = new MockSettingsService();
        var vm = new MicrophoneSettingsViewModel(enumerator, settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Initial state with default selected
        var view1 = new MicrophoneSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Three microphones available. The built-in microphone is selected by default.",
            screenshot1));

        // Step 2: User selects a different microphone
        vm.SelectMicrophoneCommand.Execute(vm.AvailableMicrophones[1]);
        await SettleAsync();

        var view2 = new MicrophoneSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects \"USB Condenser Mic\". The selection indicator moves to the second item.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Multiple Microphones",
            "User has three microphones and switches selection from the default to a USB mic.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_NoMicrophones()
    {
        var enumerator = new MockMicrophoneEnumerator();
        var settings = new MockSettingsService();
        var vm = new MicrophoneSettingsViewModel(enumerator, settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new MicrophoneSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "No microphones detected. The list is empty — only the header and description are visible.",
            screenshot));

        _report.AddScenario(new Scenario(
            "No Microphones Detected",
            "User has no audio input devices. The microphone list is empty.",
            steps));
    }
}
