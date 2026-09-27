using Avalonia.Headless.XUnit;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class InterfaceLanguageScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "interface-language-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Interface Language — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Renders Settings → Appearance → Interface language (ADR-069). The page grew
/// from four rows to twenty-six, so it is the one surface where adding languages
/// is a visual change rather than just a resx change — the reason it now has a
/// screenshot report of its own.
/// </summary>
public class InterfaceLanguageScreenshotTests(InterfaceLanguageScreenshotReportFixture report)
    : IClassFixture<InterfaceLanguageScreenshotReportFixture>
{
    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_EveryShippedLanguage()
    {
        var vm = new InterfaceLanguageSettingsViewModel(new UiLanguageService(new MockSettingsService()));
        await SettleAsync();

        var steps = new List<ScenarioStep>
        {
            new(
                "Every shipped interface language. \"System default\" is pinned above a separator "
                + "because it names a behaviour rather than a language; the rest are sorted by "
                + "endonym under the invariant culture, so the order never shifts when the user "
                + "switches language.",
                await ScreenshotHelper.CaptureBase64Async(new InterfaceLanguageSettingsView(), vm)),
        };

        report.AddScenario(new Scenario(
            "Every Shipped Language",
            "The full picker, system default pinned, languages sorted by endonym.",
            steps));
    }
}
