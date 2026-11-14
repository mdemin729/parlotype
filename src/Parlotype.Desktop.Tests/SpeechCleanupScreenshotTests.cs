using Avalonia.Headless.XUnit;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class SpeechCleanupScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "speech-cleanup-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Speech Cleanup Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class SpeechCleanupScreenshotTests : IClassFixture<SpeechCleanupScreenshotReportFixture>
{
    private readonly SpeechCleanupScreenshotReportFixture _report;

    public SpeechCleanupScreenshotTests(SpeechCleanupScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private sealed record Rig(
        SpeechCleanupSettingsViewModel ViewModel,
        MockSpeechCleanupModelInstaller Model,
        MockLlamaServerCatalog Catalog);

    private static async Task<Rig> BuildAsync(Action<MockSettingsService>? configure = null, bool installed = false)
    {
        var settings = new MockSettingsService();
        configure?.Invoke(settings);
        var model = new MockSpeechCleanupModelInstaller { Installed = installed };
        var registry = new MockLlamaServerRegistry();
        if (installed)
        {
            registry.AddManaged("b200-win-cpu-x64");
            await registry.SetActiveAsync("b200-win-cpu-x64", LlamaServerSource.Managed);
        }

        var catalog = new MockLlamaServerCatalog();
        var vm = new SpeechCleanupSettingsViewModel(
            settings,
            new MockSpeechCleanupService(model, registry),
            model,
            registry,
            catalog,
            new MockLlamaServerInstaller(registry));
        await vm.Initialization;
        return new Rig(vm, model, catalog);
    }

    [AvaloniaFact]
    public async Task Scenario_OffAndNotSetUp()
    {
        var rig = await BuildAsync();
        await SettleAsync();

        var view = new SpeechCleanupSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, rig.ViewModel);

        Assert.False(rig.ViewModel.IsEnabled);
        _report.AddScenario(new Scenario(
            "Off, nothing installed",
            "First visit. The switch is off and the status line says what turning it on will download.",
            [
                new ScenarioStep(
                    "Switch off. Status names the model size and the llama.cpp server. The model row " +
                    "shows 'S1-mini by Superwhisper', 462 MB and a Download button. Semi-formal is selected.",
                    screenshot),
            ]));
    }

    [AvaloniaFact]
    public async Task Scenario_OnAndReady_FormalListsEmail()
    {
        var rig = await BuildAsync(
            s =>
            {
                s.SetAsync(SettingsKeys.SpeechCleanupEnabled, "True").GetAwaiter().GetResult();
                s.SetAsync(SettingsKeys.SpeechCleanupStyling, nameof(CleanupStyling.Formal)).GetAwaiter().GetResult();
                s.SetAsync(SettingsKeys.SpeechCleanupStructure, nameof(CleanupStructure.Lists)).GetAwaiter().GetResult();
                s.SetAsync(SettingsKeys.SpeechCleanupContext, nameof(CleanupContext.Email)).GetAwaiter().GetResult();
            },
            installed: true);
        await SettleAsync();

        var view = new SpeechCleanupSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, rig.ViewModel);

        Assert.True(rig.ViewModel.IsEnabled);
        _report.AddScenario(new Scenario(
            "On and ready",
            "Everything installed, cleanup on, with Formal style, lists and e-mail layout chosen.",
            [
                new ScenarioStep(
                    "Switch on, status 'Ready'. The model row shows the Installed badge and a Delete " +
                    "button. Formal carries the accent bar; both layout switches are on.",
                    screenshot),
            ]));
    }

    [AvaloniaFact]
    public async Task Scenario_SetupFailed()
    {
        var rig = await BuildAsync();
        rig.Catalog.FetchError = new HttpRequestException("No such host is known. (api.github.com:443)");
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var before = new SpeechCleanupSettingsView();
        steps.Add(new ScenarioStep(
            "Before: off, nothing installed.",
            await ScreenshotHelper.CaptureBase64Async(before, rig.ViewModel)));

        rig.ViewModel.IsEnabled = true;
        await rig.ViewModel.PendingToggle;
        await SettleAsync();

        var after = new SpeechCleanupSettingsView();
        steps.Add(new ScenarioStep(
            "The user turned the switch on while offline. The model downloaded, the server list " +
            "could not be read: the switch is back off, the status says setup did not finish, and " +
            "the system's own error text is shown under it.",
            await ScreenshotHelper.CaptureBase64Async(after, rig.ViewModel)));

        Assert.False(rig.ViewModel.IsEnabled);
        Assert.NotNull(rig.ViewModel.ErrorDetail);
        _report.AddScenario(new Scenario(
            "Setup failed",
            "Turning the switch on could not fetch a llama.cpp server.",
            steps));
    }
}
