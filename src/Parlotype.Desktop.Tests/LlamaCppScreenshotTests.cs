using Avalonia.Headless.XUnit;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class LlamaCppScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "llamacpp-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "llama.cpp Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class LlamaCppScreenshotTests : IClassFixture<LlamaCppScreenshotReportFixture>
{
    private readonly LlamaCppScreenshotReportFixture _report;

    public LlamaCppScreenshotTests(LlamaCppScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_InitialDisconnected()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new LlamaCppSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Initial state: No llama-server running. Status shows \"Not probed\". " +
            "Port defaults to 8321. Server binary path is shown. " +
            "User can click Refresh to probe the server, or change the port/path.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Initial State — Not Probed",
            "Settings page opened for the first time. No server connection attempted yet.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_RefreshDisconnected()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);
        // Use a port that definitely has nothing listening
        vm.PortText = "19876";
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Before refresh
        var view1 = new LlamaCppSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Port changed to 19876 (nothing running). Status is \"Not probed\".",
            screenshot1));

        // Step 2: After refresh — should show Disconnected
        await vm.RefreshServerInfoCommand.ExecuteAsync(null);
        await SettleAsync();

        var view2 = new LlamaCppSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "After clicking Refresh: status shows \"Disconnected\" with a gray indicator. " +
            "No server properties are displayed. The port and path fields remain editable.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Refresh — Server Not Running",
            "User clicks Refresh when no llama-server is running on the configured port.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_PortConflict()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Simulate port conflict by setting state directly
        vm.StatusText = "Port conflict";
        vm.HasPortConflict = true;
        vm.IsConnected = false;
        vm.ErrorMessage = "Port 8321 is in use by another application (not llama-server)";
        await SettleAsync();

        var view = new LlamaCppSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Port conflict detected: another application (not llama-server) is using port 8321. " +
            "An orange warning banner is displayed with the error message and guidance to change the port.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Port Conflict",
            "The configured port is occupied by a non-llama-server process (e.g., Python sidecar, LM Studio).",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_Connected()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Simulate connected state with server properties
        vm.StatusText = "Connected";
        vm.IsConnected = true;
        vm.HasPortConflict = false;
        vm.ModelAlias = "gemma-4-E4B-it-Q4_K_M.gguf";
        vm.ModelPath = @"C:\Users\Maksim\AppData\Local\parlotype\models\gemma-4-E4B-it-Q4_K_M.gguf";
        vm.AudioSupported = true;
        vm.BuildInfo = "b9090-5757c4dcb";
        await SettleAsync();

        var view = new LlamaCppSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Connected to llama-server. Green status indicator. " +
            "Server properties displayed: Model (gemma-4-E4B-it-Q4_K_M.gguf), " +
            "full model path, Audio support confirmed, and build version (b9090).",
            screenshot));

        _report.AddScenario(new Scenario(
            "Connected — Server Properties Displayed",
            "llama-server is running and healthy. All server properties are shown in a clean form layout.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ChangePort()
    {
        var settings = new MockSettingsService();
        var vm = new LlamaCppSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: Default port
        var view1 = new LlamaCppSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Default port is 8321.",
            screenshot1));

        // Step 2: Change port
        vm.PortText = "9090";
        await SettleAsync();

        var view2 = new LlamaCppSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User changes the port to 9090. The Save button is available to persist the change.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Change Server Port",
            "User changes the llama-server port from the default 8321 to a custom port.",
            steps));
    }
}
