using Avalonia.Headless.XUnit;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Fixture that collects scenarios from all tests in the class and generates
/// an HTML report when the test class is disposed.
/// </summary>
public sealed class ScreenshotReportFixture : IAsyncLifetime
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
            var reportPath = Path.Combine(FindRepoRoot(), "reports", "runtime-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Runtime Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }

    internal static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir, "Parlotype.slnx")))
                return dir;
            dir = Path.GetDirectoryName(dir);
        }

        return Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..");
    }
}

public class RuntimeSettingsScreenshotTests : IClassFixture<ScreenshotReportFixture>
{
    private readonly ScreenshotReportFixture _report;

    public RuntimeSettingsScreenshotTests(ScreenshotReportFixture report)
    {
        _report = report;
    }

    private static RuntimeSettingsViewModel BuildVm(VulkanEnvironmentInfo? vulkan = null)
    {
        var settings = new MockSettingsService();
        var vulkanProvider = new MockVulkanEnvironmentProvider(
            vulkan ?? new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" });
        return new RuntimeSettingsViewModel(settings, vulkanProvider);
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_VulkanLoaderMissing()
    {
        var vm = BuildVm(vulkan: VulkanEnvironmentInfo.Empty);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Initial state — Vulkan unavailable
        var view = new RuntimeSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Initial state: Vulkan loader (vulkan-1.dll) is not detected. Vulkan is marked unavailable. The guidance panel shows a download button for the Vulkan SDK.",
            screenshot1));

        // Step 2: User selects Vulkan
        vm.SelectRuntimeCommand.Execute(RuntimePreference.Vulkan);
        await SettleAsync();

        var view2 = new RuntimeSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects Vulkan despite it being unavailable. The warning and download guidance remain visible.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Vulkan — Loader Missing",
            "User's GPU drivers don't include Vulkan support. The Vulkan option is dimmed with a download link to the Vulkan SDK.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_AllAvailable()
    {
        var vm = BuildVm(
            vulkan: new VulkanEnvironmentInfo
            {
                HasVulkanLoader = true,
                LoaderVersion = "1.3.268",
                SdkInstalled = true,
                SdkPath = @"C:\VulkanSDK\1.3.268.0",
            });
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: All runtimes available — no warnings
        var view = new RuntimeSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Happy path: Auto, Vulkan (loader 1.3.268 with SDK) and CPU are all selectable with no warnings.",
            screenshot1));

        // Step 2: User selects Vulkan
        vm.SelectRuntimeCommand.Execute(RuntimePreference.Vulkan);
        await SettleAsync();

        var view2 = new RuntimeSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects Vulkan. The selection indicator moves to Vulkan. No warnings or guidance panels are shown.",
            screenshot2));

        // Step 3: User falls back to CPU
        vm.SelectRuntimeCommand.Execute(RuntimePreference.Cpu);
        await SettleAsync();

        var view3 = new RuntimeSettingsView();
        var screenshot3 = await ScreenshotHelper.CaptureBase64Async(view3, vm);
        steps.Add(new ScenarioStep(
            "User selects CPU. The selection indicator moves to CPU. No warnings or guidance panels are shown.",
            screenshot3));

        _report.AddScenario(new Scenario(
            "All Runtimes Available (Happy Path)",
            "User's machine has working Vulkan support. Every option is selectable without any warnings.",
            steps));
    }
}
