using Avalonia.Headless.XUnit;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public sealed class SpeechScreenshotReportFixture : IAsyncLifetime
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
                ScreenshotReportFixture.FindRepoRoot(), "reports", "speech-settings-scenarios.html");
            ScreenshotReportGenerator.Generate(
                reportPath,
                "Speech Settings — Screenshot Test Scenarios",
                snapshot);
        }

        return ValueTask.CompletedTask;
    }
}

public class SilenceTimeoutSettingsScreenshotTests : IClassFixture<SpeechScreenshotReportFixture>
{
    private readonly SpeechScreenshotReportFixture _report;

    public SilenceTimeoutSettingsScreenshotTests(SpeechScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_DefaultSilenceTimeout()
    {
        var settings = new MockSettingsService();
        var vm = new SilenceTimeoutSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new SilenceTimeoutSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Default silence timeout: Medium selected.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Default Silence Timeout",
            "Fresh install default — Medium wait time.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_ChangeWaitTime()
    {
        var settings = new MockSettingsService();
        var vm = new SilenceTimeoutSettingsViewModel(settings);
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view1 = new SilenceTimeoutSettingsView();
        var screenshot1 = await ScreenshotHelper.CaptureBase64Async(view1, vm);
        steps.Add(new ScenarioStep(
            "Initial state: Medium silence timeout is selected (default).",
            screenshot1));

        vm.SelectWaitTimeCommand.Execute(WaitTimeOption.Long);
        await SettleAsync();

        var view2 = new SilenceTimeoutSettingsView();
        var screenshot2 = await ScreenshotHelper.CaptureBase64Async(view2, vm);
        steps.Add(new ScenarioStep(
            "User selects Long silence timeout. The selection indicator moves to \"Long\".",
            screenshot2));

        _report.AddScenario(new Scenario(
            "Change Silence Timeout",
            "User changes the silence timeout from Medium to Long.",
            steps));
    }
}

public class WhisperOutputSettingsScreenshotTests : IClassFixture<SpeechScreenshotReportFixture>
{
    private readonly SpeechScreenshotReportFixture _report;

    public WhisperOutputSettingsScreenshotTests(SpeechScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    [AvaloniaFact]
    public async Task Scenario_PunctuationAndProfanityOn()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.AutomaticPunctuationEnabled = true;
        vm.FilterProfanityEnabled = true;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new WhisperOutputSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Whisper output options enabled: punctuation on, profanity filter on. " +
            "(Translation lives on the Language settings page from now on.)",
            screenshot));

        _report.AddScenario(new Scenario(
            "Whisper Output — Toggles Enabled",
            "User enables punctuation and profanity filter.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_PunctuationAndProfanityOff()
    {
        var settings = new MockSettingsService();
        var vm = new WhisperOutputSettingsViewModel(settings);
        await SettleAsync();

        vm.AutomaticPunctuationEnabled = false;
        vm.FilterProfanityEnabled = false;
        await SettleAsync();

        var steps = new List<ScenarioStep>();

        var view = new WhisperOutputSettingsView();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(view, vm);
        steps.Add(new ScenarioStep(
            "Whisper output options disabled: punctuation stripped, profanity filter off.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Whisper Output — Toggles Disabled",
            "User disables punctuation and profanity filter.",
            steps));
    }
}

public class LanguageSettingsScreenshotTests : IClassFixture<SpeechScreenshotReportFixture>
{
    private readonly SpeechScreenshotReportFixture _report;

    public LanguageSettingsScreenshotTests(SpeechScreenshotReportFixture report)
    {
        _report = report;
    }

    private static async Task SettleAsync()
    {
        await Task.Delay(100);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs();
    }

    private static async Task<LanguageSelectionSettingsViewModel> CreateVmAsync(
        MockSettingsService settings,
        SpeechEngine engine = SpeechEngine.Whisper,
        KeyboardLayoutInfo? layout = null)
    {
        await settings.SetAsync(SettingsKeys.SpeechEngine, engine.ToString(),
            TestContext.Current.CancellationToken);
        var relationship = new LanguageRelationshipViewModel(
            settings, new MockKeyboardLayoutService { Result = layout });
        await relationship.InitializeAsync(TestContext.Current.CancellationToken);
        var vm = new LanguageSelectionSettingsViewModel(relationship);
        await SettleAsync();
        return vm;
    }

    /// <summary>Captures the page in dark and light (NFR-5) as two scenario steps.</summary>
    private static async Task<List<ScenarioStep>> CaptureBothThemesAsync(
        LanguageSelectionSettingsViewModel vm, string caption)
    {
        var dark = await ScreenshotHelper.CaptureBase64Async(
            new LanguageSelectionSettingsView(), vm, theme: Avalonia.Styling.ThemeVariant.Dark);
        var light = await ScreenshotHelper.CaptureBase64Async(
            new LanguageSelectionSettingsView(), vm, theme: Avalonia.Styling.ThemeVariant.Light);
        return
        [
            new ScenarioStep($"{caption} (dark theme)", dark),
            new ScenarioStep($"{caption} (light theme)", light),
        ];
    }

    [AvaloniaFact]
    public async Task Scenario_Whisper_ToggleForm_TranslationOff()
    {
        var vm = await CreateVmAsync(new MockSettingsService(),
            layout: new KeyboardLayoutInfo("en", "English (United States)"));

        var steps = await CaptureBothThemesAsync(vm,
            "Default state: source = Auto-detect, toggle form (Whisper), connector shows \"=\" muted, " +
            "summary reads \"no translation\".");

        _report.AddScenario(new Scenario(
            "Language — Whisper, Translation Off",
            "Fresh install on Whisper: toggle form at rest, dark + light.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_Whisper_ToggleForm_TranslationOn()
    {
        var vm = await CreateVmAsync(new MockSettingsService());
        vm.ToggleTranslationCommand.Execute(null);
        await SettleAsync();

        var steps = await CaptureBothThemesAsync(vm,
            "Translation flipped on: connector becomes an accent \"→\", switch is checked, " +
            "summary reads \"Parlotype types English\".");

        _report.AddScenario(new Scenario(
            "Language — Whisper, Translation On",
            "One-action flip on the toggle form, dark + light.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_KeyboardSource_WithDetectedLayout()
    {
        var vm = await CreateVmAsync(new MockSettingsService(),
            layout: new KeyboardLayoutInfo("ru", "Russian (Russia)"));
        vm.OpenSourcePickerCommand.Execute(null);
        vm.SourcePicker.SelectCommand.Execute(LanguageCatalog.KeyboardLayoutCode);
        await SettleAsync();

        var steps = await CaptureBothThemesAsync(vm,
            "Source = System keyboard layout with the detected layout named in the sub-hint; " +
            "the summary uses the detected language.");

        _report.AddScenario(new Scenario(
            "Language — Keyboard-Layout Source",
            "The new default-friendly source state with its Detected sub-hint.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_Gemma4_FullForm_TargetRussian()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "ru",
            TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString(),
            TestContext.Current.CancellationToken);
        var vm = await CreateVmAsync(settings, SpeechEngine.Gemma4);

        var steps = await CaptureBothThemesAsync(vm,
            "Gemma 4: full form with the target picker field showing Russian; connector on.");

        _report.AddScenario(new Scenario(
            "Language — Gemma 4 Full Form",
            "Arbitrary translation target on the full form, dark + light.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_TranscribeOnly_NoneForm_AmberNote()
    {
        var vm = await CreateVmAsync(new MockSettingsService());
        vm.UpdateForEngine((SpeechEngine)999); // capability fallback ⇒ TranslationForm.None
        await SettleAsync();

        var steps = await CaptureBothThemesAsync(vm,
            "Translation unavailable: target card disabled, connector locked at \"=\" (50%), " +
            "amber note names the model.");

        _report.AddScenario(new Scenario(
            "Language — Translation Unavailable (none form)",
            "Forward-looking transcribe-only engine state, dark + light.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_EngineSwitch_FallbackToast()
    {
        var vm = await CreateVmAsync(new MockSettingsService(), SpeechEngine.Gemma4);
        vm.OpenSourcePickerCommand.Execute(null);
        vm.SourcePicker.SelectCommand.Execute("ga"); // Irish — not a Whisper source
        vm.UpdateForEngine(SpeechEngine.Whisper);
        await SettleAsync();

        var steps = new List<ScenarioStep>();
        var screenshot = await ScreenshotHelper.CaptureBase64Async(
            new LanguageSelectionSettingsView(), vm, theme: Avalonia.Styling.ThemeVariant.Dark);
        steps.Add(new ScenarioStep(
            "Engine switched to Whisper while the source was Irish: source fell back to the keyboard " +
            "layout and the accent-soft toast explains the change.",
            screenshot));

        _report.AddScenario(new Scenario(
            "Language — Engine-Switch Fallback Toast",
            "Spec §8: unsupported selections fall back and explain via a one-line toast.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_TranslationPaused_OnNonTranslatingWhisperModel()
    {
        var vm = await CreateVmAsync(new MockSettingsService());
        vm.ToggleTranslationCommand.Execute(null);
        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);
        await SettleAsync();

        var steps = await CaptureBothThemesAsync(vm,
            "Translation is on but Large v3 Turbo can't translate (ADR-061). The connector turns " +
            "amber at \"=\", the target card carries a \"Paused\" sub-line, the summary says the " +
            "output matches the spoken language, and the amber banner names the model and offers " +
            "the model page. The switch stays on and operable — the preference is preserved.");

        _report.AddScenario(new Scenario(
            "Language — Translation Paused (amber, reversible)",
            "Large v3 Turbo + translation on, dark + light.",
            steps));
    }

    [AvaloniaFact]
    public async Task Scenario_PickerPopoverContent()
    {
        var vm = await CreateVmAsync(new MockSettingsService(),
            layout: new KeyboardLayoutInfo("en", "English (United States)"));
        vm.OpenSourcePickerCommand.Execute(null);
        await SettleAsync();

        // The popover renders in its own popup layer, which headless window
        // capture can't see — so screenshot the picker content directly.
        var steps = new List<ScenarioStep>();
        var atRest = await ScreenshotHelper.CaptureBase64Async(
            new LanguagePickerView(), vm.SourcePicker, width: 340,
            theme: Avalonia.Styling.ThemeVariant.Dark);
        steps.Add(new ScenarioStep(
            "Source popover at rest: keyboard + auto specials with sub-hints, search box, " +
            "\"All languages\" group with icon tiles and native subnames.",
            atRest));

        vm.SourcePicker.Filter = "russ";
        await SettleAsync();
        var filtered = await ScreenshotHelper.CaptureBase64Async(
            new LanguagePickerView(), vm.SourcePicker, width: 340,
            theme: Avalonia.Styling.ThemeVariant.Dark);
        steps.Add(new ScenarioStep(
            "Typing narrows to matching languages; specials and group headers hide while searching.",
            filtered));

        vm.SourcePicker.Filter = "zzzz";
        await SettleAsync();
        var empty = await ScreenshotHelper.CaptureBase64Async(
            new LanguagePickerView(), vm.SourcePicker, width: 340,
            theme: Avalonia.Styling.ThemeVariant.Dark);
        steps.Add(new ScenarioStep(
            "No matches: centred search icon and the query named in the empty state.",
            empty));

        _report.AddScenario(new Scenario(
            "Language — Picker Popover",
            "Popover content: specials, search, groups, and the empty state.",
            steps));
    }
}
