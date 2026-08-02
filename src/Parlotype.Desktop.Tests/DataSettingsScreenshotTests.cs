using Avalonia.Headless.XUnit;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class DataScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "data-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Data Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Renders Settings → Application → Data. Beyond producing the visual report,
/// these are the only tests that actually load <c>DataSettingsView.axaml</c> —
/// compiled bindings are checked at build time, but control and converter
/// resolution (<c>SelectableTextBlock</c>, <c>ObjectConverters.IsNotNull</c>)
/// only fails when the XAML is loaded.
/// </summary>
public class DataSettingsScreenshotTests : IClassFixture<DataScreenshotReportFixture>
{
    private readonly DataScreenshotReportFixture _report;

    public DataSettingsScreenshotTests(DataScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultState()
    {
        using var paths = new MockAppPaths();
        var vm = new DataSettingsViewModel(
            new MockSettingsService(), paths, new MockUserDialogService(), new MockShellService());
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new DataSettingsView();
        steps.Add(new ScenarioStep(
            "Fresh install: the data location is shown with Copy path / Open folder beside it, "
            + "no models are downloaded yet, and uninstall keeps user data (the safe default).",
            await ScreenshotHelper.CaptureBase64Async(view, vm)));

        _report.AddScenario(new Scenario(
            "Default State",
            "Nothing downloaded, uninstall cleanup off.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_CopyPathAndOptInToCleanup()
    {
        using var paths = new MockAppPaths();
        paths.WriteFakeModel(bytes: 5 * 1024 * 1024);

        var vm = new DataSettingsViewModel(
            new MockSettingsService(), paths, new MockUserDialogService(), new MockShellService());
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view1 = new DataSettingsView();
        steps.Add(new ScenarioStep(
            "With models on disk, the size is reported next to \"Downloaded models\".",
            await ScreenshotHelper.CaptureBase64Async(view1, vm)));

        await vm.CopyPathCommand.ExecuteAsync(null);
        await SettleAsync();

        var view2 = new DataSettingsView();
        steps.Add(new ScenarioStep(
            "After pressing Copy path, a confirmation line appears under the buttons.",
            await ScreenshotHelper.CaptureBase64Async(view2, vm)));

        vm.UninstallRemovesData = true;
        await SettleAsync();

        var view3 = new DataSettingsView();
        steps.Add(new ScenarioStep(
            "Opting into cleanup: uninstalling will now also remove models, settings and API keys. "
            + "This toggle is the consent the uninstall hook reads, since the hook itself cannot show UI.",
            await ScreenshotHelper.CaptureBase64Async(view3, vm)));

        _report.AddScenario(new Scenario(
            "Copy Path and Opt Into Cleanup",
            "Models present, path copied, uninstall cleanup switched on.",
            steps));
    }
}
