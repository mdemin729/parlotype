using Avalonia.Headless.XUnit;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class Gemma4ModelScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "gemma4-model-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Gemma 4 Model Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class Gemma4ModelSettingsScreenshotTests : IClassFixture<Gemma4ModelScreenshotReportFixture>
{
    private readonly Gemma4ModelScreenshotReportFixture _report;

    public Gemma4ModelSettingsScreenshotTests(Gemma4ModelScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultNeitherInstalled()
    {
        var settings = new MockSettingsService();
        var vm = new Gemma4ModelSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new Gemma4ModelSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Five models are listed (E2B Q8_0/BF16, E4B Q4_K_M/Q8_0/BF16). E4B Q4_K_M is selected " +
            "by default. None are installed, so each shows a Download button.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default — no models installed",
            "Fresh install — E4B Q4_K_M selected, all five models offer Download.",
            steps));

        Assert.Equal(Gemma4ModelInfo.Default.ModelId, vm.SelectedModelId);
    }
}
