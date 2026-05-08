using Avalonia.Headless.XUnit;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class ThemeScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "theme-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Theme Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class ThemeSettingsScreenshotTests : IClassFixture<ThemeScreenshotReportFixture>
{
    private readonly ThemeScreenshotReportFixture _report;

    public ThemeSettingsScreenshotTests(ThemeScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultTheme()
    {
        var settings = new MockSettingsService();
        var vm = new ThemeSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new ThemeSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default theme: \"Default (system)\" is the initial selection. Three options are shown: Default (system), Light, and Dark.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default Theme",
            "Fresh install — system theme is selected by default.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_SwitchThemes()
    {
        var settings = new MockSettingsService();
        var vm = new ThemeSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Default
        var view1 = new ThemeSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Initial state: Default (system) theme selected.",
            screenshot1));

        // Step 2: Select Light
        vm.SelectThemeCommand.Execute(AppTheme.Light);
        await SettleAsync();

        var view2 = new ThemeSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects Light theme.",
            screenshot2));

        // Step 3: Select Dark
        vm.SelectThemeCommand.Execute(AppTheme.Dark);
        await SettleAsync();

        var view3 = new ThemeSettingsView();
        var screenshot3 = await ScreenshotHelper.CaptureBase64Async(view3, vm);
        steps.Add(new ScenarioStep(
            "User selects Dark theme.",
            screenshot3));

        _report.AddScenario(new Scenario(
            "Switch Through All Themes",
            "User cycles through Default → Light → Dark themes.",
            steps));
    }
}
