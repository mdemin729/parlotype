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

    private static RuntimeSettingsViewModel BuildVm(
        NvidiaEnvironmentInfo? nvidia = null,
        VulkanEnvironmentInfo? vulkan = null)
    {
        var settings = new MockSettingsService();
        var nvidiaProvider = new MockNvidiaEnvironmentProvider(nvidia ?? NvidiaEnvironmentInfo.Empty);
        var vulkanProvider = new MockVulkanEnvironmentProvider(
            vulkan ?? new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" });
        return new RuntimeSettingsViewModel(settings, nvidiaProvider, vulkanProvider);
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_CudaNoDriver()
    {
        var vm = BuildVm(
            nvidia: NvidiaEnvironmentInfo.Empty,
            vulkan: new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" });
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Initial state — CUDA shows as unavailable
        var view = new RuntimeSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Initial state: Runtime settings opened. CUDA is marked unavailable because no NVIDIA GPU was detected.",
            screenshot1));

        // Step 2: User selects CUDA — the guidance panel is visible
        vm.SelectRuntimeCommand.Execute(RuntimePreference.Cuda);
        await SettleAsync();

        var view2 = new RuntimeSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects CUDA runtime. The \"NVIDIA GPU not detected\" guidance panel is displayed, explaining that CUDA requires an NVIDIA GPU with drivers installed.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "CUDA — No NVIDIA Driver",
            "User's machine has no NVIDIA GPU or driver installed. Selecting CUDA shows a clear explanation.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_CudaDriverButNoSdk()
    {
        var vm = BuildVm(
            nvidia: new NvidiaEnvironmentInfo
            {
                DriverVersion = "596.36",
                DriverMaxCudaVersion = "13.2",
                LoadableRuntimes = [],
            },
            vulkan: new VulkanEnvironmentInfo { HasVulkanLoader = true, LoaderVersion = "1.3.0" });
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Initial state — CUDA shows unavailable with SDK message
        var view = new RuntimeSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Initial state: NVIDIA driver (version 596.36) is detected, but the CUDA toolkit is not installed. CUDA is marked unavailable.",
            screenshot1));

        // Step 2: User selects CUDA
        vm.SelectRuntimeCommand.Execute(RuntimePreference.Cuda);
        await SettleAsync();

        var view2 = new RuntimeSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects CUDA. The guidance panel shows the detected driver version (596.36) and provides step-by-step instructions to install the CUDA toolkit, including a clickable \"Download CUDA Toolkit\" button.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "CUDA — Driver Present, Toolkit Missing",
            "User has an NVIDIA GPU with driver 596.36 but hasn't installed the CUDA toolkit. The UI shows the driver version and provides download instructions.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_VulkanLoaderMissing()
    {
        var vm = BuildVm(
            nvidia: new NvidiaEnvironmentInfo
            {
                DriverVersion = "596.36",
                LoadableRuntimes = [new CudaRuntimeProbe("cudart64_13", "13.2", "13.2")],
            },
            vulkan: VulkanEnvironmentInfo.Empty);
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
            nvidia: new NvidiaEnvironmentInfo
            {
                DriverVersion = "596.36",
                LoadableRuntimes = [new CudaRuntimeProbe("cudart64_13", "13.2", "13.2")],
            },
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
            "Happy path: All runtimes are available. CUDA (NVIDIA driver 596.36 with toolkit), Vulkan (loader 1.3.268 with SDK), and CPU are all selectable with no warnings.",
            screenshot1));

        // Step 2: User selects CUDA
        vm.SelectRuntimeCommand.Execute(RuntimePreference.Cuda);
        await SettleAsync();

        var view2 = new RuntimeSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects CUDA. The selection indicator moves to CUDA. No warnings or guidance panels are shown.",
            screenshot2));

        // Step 3: User selects Vulkan
        vm.SelectRuntimeCommand.Execute(RuntimePreference.Vulkan);
        await SettleAsync();

        var view3 = new RuntimeSettingsView();
        var screenshot3 = await ScreenshotHelper.CaptureBase64Async(view3, vm);
        steps.Add(new ScenarioStep(
            "User selects Vulkan. The selection indicator moves to Vulkan. No warnings or guidance panels are shown.",
            screenshot3));

        _report.AddScenario(new Scenario(
            "All Runtimes Available (Happy Path)",
            "User's machine has full CUDA and Vulkan support. All options are selectable without any warnings.",
            steps));
    }
}
