using Avalonia.Headless.XUnit;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class CloudProviderScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "cloud-provider-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Cloud Provider Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class CloudProviderScreenshotTests : IClassFixture<CloudProviderScreenshotReportFixture>
{
    private readonly CloudProviderScreenshotReportFixture _report;

    public CloudProviderScreenshotTests(CloudProviderScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultNoKey()
    {
        var settings = new MockSettingsService();
        var secrets = new MockSecretStore();
        var vm = new CloudProviderSettingsViewModel(settings, secrets);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new CloudProviderSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default state: no base URL, model, or API key has been configured for either provider. " +
            "Both text fields show their default value as a watermark placeholder, and both key rows show " +
            "the editable password entry (with a show/hide eye toggle) and a Save button.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default — No Key Configured",
            "Fresh install. Neither cloud provider has been configured yet.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_KeySaved()
    {
        var settings = new MockSettingsService();
        var secrets = new MockSecretStore();
        var vm = new CloudProviderSettingsViewModel(settings, secrets);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        // Step 1: default state
        var view1 = new CloudProviderSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Initial state: no keys saved.",
            screenshot1));

        // Step 2: user types a key and saves it
        vm.OpenAiKeyEntry = "sk-example-key-1234567890";
        await SettleAsync();

        var view2 = new CloudProviderSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User types an OpenAI-compatible API key into the write-only entry field (masked with PasswordChar).",
            screenshot2));

        await vm.SaveOpenAiKeyCommand.ExecuteAsync(null);
        await SettleAsync();

        var view3 = new CloudProviderSettingsView();
        var screenshot3 = await ScreenshotHelper.CaptureBase64Async(view3, vm);
        steps.Add(new ScenarioStep(
            "User clicks Save. The key is written to the secret store; the OpenAI-compatible row flips to its " +
            "saved state — a read-only ●●●● mask (fixed length, never the real key), an accent-colored " +
            "\"✓ Saved\" badge, and Change / Remove buttons.",
            screenshot3));

        _report.AddScenario(new Scenario(
            "Save an API Key",
            "User enters and saves an OpenAI-compatible API key. The row switches to the masked saved state.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ChangeExistingKey()
    {
        var ct = TestContext.Current.CancellationToken;
        var secrets = new MockSecretStore();
        await secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, "sk-existing-key", ct);
        var vm = new CloudProviderSettingsViewModel(new MockSettingsService(), secrets);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view1 = new CloudProviderSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Saved state: OpenAI-compatible shows the masked ●●●● display, the \"✓ Saved\" badge, and " +
            "Change / Remove. xAI Grok has no key, so it shows the entry field.",
            screenshot1));

        vm.ChangeOpenAiKeyCommand.Execute(null);
        await SettleAsync();

        var view2 = new CloudProviderSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User clicks Change. The OpenAI-compatible row returns to the editable entry field (eye toggle + Save), " +
            "now with a Cancel button that abandons the replacement and keeps the stored key.",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Change an Existing Key",
            "Replacing a stored key: Change reveals the entry with a Cancel fallback; the old key stays until a new one is saved.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ValuesFilled()
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.OpenAiCompatBaseUrl, "https://api.groq.com/openai/v1", ct);
        await settings.SetAsync(SettingsKeys.OpenAiCompatModel, "whisper-large-v3", ct);
        await settings.SetAsync(SettingsKeys.XaiGrokBaseUrl, "https://api.x.ai/v1", ct);
        await settings.SetAsync(SettingsKeys.XaiGrokModel, "grok-stt", ct);

        var secrets = new MockSecretStore();
        await secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, "sk-existing-key", ct);
        await secrets.SetAsync(SettingsKeys.XaiGrokApiKey, "xai-existing-key", ct);

        var vm = new CloudProviderSettingsViewModel(settings, secrets);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new CloudProviderSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Fully configured state: both providers have a custom base URL and model persisted " +
            "(OpenAI-compatible pointed at Groq; xAI Grok at its default), and both show the masked " +
            "saved state with a \"✓ Saved\" badge and Change / Remove buttons. " +
            "The raw key values are never shown — only the fixed-length mask.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Fully Configured — Both Providers",
            "Both providers have base URL, model, and API key already configured (e.g. after a restart).",
            steps));
    }
}
