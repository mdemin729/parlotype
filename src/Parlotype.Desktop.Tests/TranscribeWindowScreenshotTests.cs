using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Parlotype.Core.Settings;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Speech;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class TranscribeScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "transcribe-window-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Transcribe Window — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

/// <summary>
/// Phase 2 strip states (on / off / locked) in dark and light. The flyout opens
/// in a popup layer the headless window capture can't see; its content is the
/// shared <c>LanguagePickerView</c> already covered by the Language page report.
/// </summary>
public class TranscribeWindowScreenshotTests : IClassFixture<TranscribeScreenshotReportFixture>
{
    private readonly TranscribeScreenshotReportFixture _report;

    public TranscribeWindowScreenshotTests(TranscribeScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task<TranscribeViewModel> CreateVmAsync(
        SpeechEngine engine = SpeechEngine.Whisper,
        KeyboardLayoutInfo? layout = null)
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, engine.ToString(),
            TestContext.Current.CancellationToken);
        var relationship = new LanguageRelationshipViewModel(
            settings, new MockKeyboardLayoutService { Result = layout });
        await relationship.InitializeAsync(TestContext.Current.CancellationToken);
        return new TranscribeViewModel(new MockWindowManager(), relationship: relationship);
    }

    /// <summary>Renders the real TranscribeWindow and captures it as base64 PNG.</summary>
    private static async Task<string> CaptureWindowAsync(TranscribeViewModel vm, ThemeVariant theme)
    {
        var window = new TranscribeWindow
        {
            DataContext = vm,
            RequestedThemeVariant = theme,
        };
        window.Show();

        await Task.Delay(150);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();

        var bitmap = window.CaptureRenderedFrame();
        window.Close();
        if (bitmap is null)
            return string.Empty;

        using var ms = new MemoryStream();
        bitmap.Save(ms);
        return Convert.ToBase64String(ms.ToArray());
    }

    private async Task AddBothThemesAsync(TranscribeViewModel vm, string title, string caption)
    {
        var steps = new List<ScenarioStep>
        {
            new($"{caption} (dark theme)", await CaptureWindowAsync(vm, ThemeVariant.Dark)),
            new($"{caption} (light theme)", await CaptureWindowAsync(vm, ThemeVariant.Light)),
        };
        _report.AddScenario(new Scenario(title, caption, steps));
    }

    [AvaloniaFact]
    public async Task Scenario_Strip_TranslationOff()
    {
        var vm = await CreateVmAsync(layout: new KeyboardLayoutInfo("en", "English (United States)"));

        await AddBothThemesAsync(vm,
            "Transcribe — Strip, Translation Off",
            "At rest: the strip reads \"Auto = Auto\" — output matches the spoken language.");
    }

    [AvaloniaFact]
    public async Task Scenario_Strip_TranslationOn()
    {
        var vm = await CreateVmAsync(SpeechEngine.Gemma4);
        vm.Relationship!.SelectSource("ru");
        vm.Relationship.SelectTarget("fr");

        await AddBothThemesAsync(vm,
            "Transcribe — Strip, Translation On",
            "Translating: \"Russian → French\" with the accent connector, mirroring the Settings page.");
    }

    [AvaloniaFact]
    public async Task Scenario_Strip_TranslationPaused()
    {
        var vm = await CreateVmAsync();
        vm.Relationship!.SelectSource("ru");
        vm.Relationship.ToggleTranslation();
        vm.Relationship.SetWhisperModel(WhisperModelType.LargeV3Turbo);

        await AddBothThemesAsync(vm,
            "Transcribe — Strip, Translation Paused",
            "Translation is on but the Whisper model can't translate: the connector turns amber " +
            "at \"=\" and the target chip mirrors the source, so the strip never advertises a " +
            "translation that will not happen (ADR-061).");
    }

    /// <summary>
    /// A widget whose cleanup model and server are installed, so the indicator
    /// shows (ADR-075). Returns the shared state for the scenario to drive.
    /// </summary>
    private static async Task<(TranscribeViewModel Vm, SpeechCleanupStateViewModel State)> CreateCleanupVmAsync(
        SpeechEngine engine, bool enabled)
    {
        var ct = TestContext.Current.CancellationToken;
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, engine.ToString(), ct);
        await settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, enabled.ToString(), ct);

        var model = new MockSpeechCleanupModelInstaller { Installed = true };
        var registry = new MockLlamaServerRegistry();
        registry.AddManaged("b200-win-cpu-x64");
        await registry.SetActiveAsync("b200-win-cpu-x64", LlamaServerSource.Managed, ct);

        var relationship = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await relationship.InitializeAsync(ct);
        var state = new SpeechCleanupStateViewModel(
            settings, new MockSpeechCleanupService(model, registry), relationship);
        await state.Initialization;

        var vm = new TranscribeViewModel(
            new MockWindowManager(), relationship: relationship, cleanupState: state);
        return (vm, state);
    }

    [AvaloniaFact]
    public async Task Scenario_Cleanup_On()
    {
        var (vm, state) = await CreateCleanupVmAsync(SpeechEngine.Parakeet, enabled: true);

        Assert.Equal(CleanupIndicatorState.On, state.State);
        await AddBothThemesAsync(vm,
            "Transcribe — Speech cleanup on",
            "Cleanup is set up and on: an accent sparkle sits between the grip and the close button. " +
            "The compact Parakeet widget keeps its height.");
    }

    [AvaloniaFact]
    public async Task Scenario_Cleanup_Off()
    {
        var (vm, state) = await CreateCleanupVmAsync(SpeechEngine.Parakeet, enabled: false);

        Assert.Equal(CleanupIndicatorState.Off, state.State);
        await AddBothThemesAsync(vm,
            "Transcribe — Speech cleanup off",
            "Set up but switched off: the sparkle is dimmed. One click turns cleanup back on.");
    }

    [AvaloniaFact]
    public async Task Scenario_Cleanup_PausedForLanguage()
    {
        var (vm, state) = await CreateCleanupVmAsync(SpeechEngine.Whisper, enabled: true);
        vm.Relationship!.SelectSource("ru");

        Assert.Equal(CleanupIndicatorState.Paused, state.State);
        await AddBothThemesAsync(vm,
            "Transcribe — Speech cleanup paused",
            "The dictation language is Russian, so the English-only cleanup will not run: the sparkle " +
            "turns amber, the same signal a paused translation gives on the strip below (ADR-075).");
    }

    [AvaloniaFact]
    public async Task Scenario_Cleanup_LastDictationSkipped_WithCloudBadge()
    {
        var (vm, state) = await CreateCleanupVmAsync(SpeechEngine.OpenAiCompatible, enabled: true);
        vm.SetActiveEngine(SpeechEngine.OpenAiCompatible);
        state.ReportOutcome(SpeechCleanupOutcome.SkippedNotEnglish);

        Assert.Equal(CleanupIndicatorState.Skipped, state.State);
        await AddBothThemesAsync(vm,
            "Transcribe — Last dictation pasted as spoken, cloud engine",
            "The header at its fullest: Cloud badge on the left, grip, amber sparkle and close button. " +
            "The last dictation was not English and went in unchanged.");
    }

    [AvaloniaFact]
    public async Task Scenario_Strip_Locked()
    {
        var vm = await CreateVmAsync();
        vm.Relationship!.SetEngine((SpeechEngine)999);

        await AddBothThemesAsync(vm,
            "Transcribe — Strip, Translation Unavailable",
            "Transcribe-only engine: the connector locks at \"=\" and the chips mirror the source.");
    }
}
