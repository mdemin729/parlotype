using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Xunit;
using Avalonia.Headless.XUnit;

namespace Parlotype.Desktop.Tests;

public class LanguageRelationshipViewModelTests
{
    private static async Task<(LanguageRelationshipViewModel Vm, MockSettingsService Settings, MockKeyboardLayoutService Keyboard)>
        CreateAsync(SpeechEngine engine = SpeechEngine.Whisper, KeyboardLayoutInfo? layout = null)
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, engine.ToString(), TestContext.Current.CancellationToken);
        var keyboard = new MockKeyboardLayoutService { Result = layout };
        var vm = new LanguageRelationshipViewModel(settings, keyboard);
        await vm.InitializeAsync(TestContext.Current.CancellationToken);
        return (vm, settings, keyboard);
    }

    // Engines both shipping engines translate, so the None form is exercised via
    // an out-of-range value hitting the capability fallback branch (§Spec 11
    // transcribe-only row).
    private const SpeechEngine TranscribeOnlyEngine = (SpeechEngine)999;

    // ----- Initialization ----------------------------------------------------

    [Fact]
    public async Task Init_Defaults_AutoSourceNoTranslationToggleForm()
    {
        var (vm, _, _) = await CreateAsync();

        Assert.Equal(LanguageCatalog.AutoDetectCode, vm.SourceCode);
        Assert.Equal(LanguageCatalog.NoTranslationCode, vm.TargetCode);
        Assert.False(vm.TranslationEnabled);
        Assert.Equal(TranslationForm.Toggle, vm.TargetForm);
        Assert.Equal(ConnectorState.Off, vm.Connector);
        Assert.Equal("=", vm.ConnectorGlyph);
        Assert.Null(vm.ToastMessage);
    }

    [Fact]
    public async Task Init_LoadsPersistedState()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, "ru", TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "fr", TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString(), TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.RecentTargetLanguages, new List<string> { "fr", "de" }, TestContext.Current.CancellationToken);

        var vm = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await vm.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(SpeechEngine.Gemma4, vm.Engine);
        Assert.Equal("ru", vm.SourceCode);
        Assert.Equal("fr", vm.TargetCode);
        Assert.True(vm.TranslationEnabled);
        Assert.Equal(TranslationForm.Full, vm.TargetForm);
        Assert.Equal(ConnectorState.On, vm.Connector);
        Assert.Equal("→", vm.ConnectorGlyph);
        Assert.Equal(["fr", "de"], vm.TargetRecent);
    }

    [Fact]
    public async Task Init_ReconcilesInconsistentState_Silently()
    {
        // Whisper (toggle form) with a stored French target: corrected to the
        // single fixed target without a toast — startup is not an engine switch.
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(), TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "fr", TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString(), TestContext.Current.CancellationToken);

        var vm = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await vm.InitializeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("en", vm.TargetCode);
        Assert.Null(vm.ToastMessage);
        Assert.Equal("en", await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task Init_DetectsKeyboardLayout()
    {
        var layout = new KeyboardLayoutInfo("en", "English (United States)");
        var (vm, _, keyboard) = await CreateAsync(layout: layout);

        Assert.Equal(layout, vm.DetectedKeyboardLayout);
        Assert.True(keyboard.DetectCalls >= 1);
    }

    // ----- Toggle translation (spec §7: 1-action flip) -----------------------

    [Fact]
    public async Task ToggleTranslation_FirstEnable_Whisper_DefaultsToEnglish()
    {
        var (vm, settings, _) = await CreateAsync();

        vm.ToggleTranslation();

        Assert.True(vm.TranslationEnabled);
        Assert.Equal("en", vm.TargetCode);
        Assert.Equal(ConnectorState.On, vm.Connector);
        Assert.Equal(true.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled, TestContext.Current.CancellationToken));
        Assert.Equal("en", await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ToggleTranslation_FirstEnable_Gemma_PrefersMostRecentTarget()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.RecentTargetLanguages, new List<string> { "de", "fr" }, TestContext.Current.CancellationToken);
        var vm = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await vm.InitializeAsync(TestContext.Current.CancellationToken);

        vm.ToggleTranslation();

        Assert.True(vm.TranslationEnabled);
        Assert.Equal("de", vm.TargetCode);
    }

    [Fact]
    public async Task ToggleTranslation_OffAndOn_RestoresLastTarget()
    {
        var (vm, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectTarget("fr");

        vm.ToggleTranslation(); // off
        Assert.False(vm.TranslationEnabled);
        Assert.Equal("fr", vm.TargetCode); // resting target survives

        vm.ToggleTranslation(); // on again — no re-asking
        Assert.True(vm.TranslationEnabled);
        Assert.Equal("fr", vm.TargetCode);
    }

    [Fact]
    public async Task ToggleTranslation_NoOp_WhenEngineCannotTranslate()
    {
        var (vm, _, _) = await CreateAsync();
        vm.SetEngine(TranscribeOnlyEngine);

        vm.ToggleTranslation();

        Assert.False(vm.TranslationEnabled);
        Assert.Equal(ConnectorState.Locked, vm.Connector);
    }

    [Fact]
    public async Task ToggleTranslation_RaisesRelationshipChanged()
    {
        var (vm, _, _) = await CreateAsync();
        var raised = 0;
        vm.RelationshipChanged += (_, _) => raised++;

        vm.ToggleTranslation();

        Assert.Equal(1, raised);
    }

    // ----- Source selection ---------------------------------------------------

    [Fact]
    public async Task SelectSource_ExplicitLanguage_PersistsAndPromotesMru()
    {
        var (vm, settings, _) = await CreateAsync();

        vm.SelectSource("ru");

        Assert.Equal("ru", vm.SourceCode);
        Assert.Equal(["ru"], vm.SourceRecent);
        Assert.Equal("ru", await settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, TestContext.Current.CancellationToken));
        Assert.Equal(["ru"], await settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectSource_KeyboardSentinel_PersistsSentinel_NotInMru()
    {
        var (vm, settings, _) = await CreateAsync(
            layout: new KeyboardLayoutInfo("en", "English (United States)"));

        vm.SelectSource(LanguageCatalog.KeyboardLayoutCode);

        Assert.Equal(LanguageCatalog.KeyboardLayoutCode, vm.SourceCode);
        Assert.Empty(vm.SourceRecent);
        Assert.Equal(LanguageCatalog.KeyboardLayoutCode,
            await settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, TestContext.Current.CancellationToken));
        Assert.Equal("System keyboard layout", vm.SourceDisplayLabel);
        Assert.Equal("Detected: English (United States)", vm.SourceSubHint);
    }

    [Fact]
    public async Task SourceSubHint_DegradesGracefully_WhenDetectionUnavailable()
    {
        var (vm, _, _) = await CreateAsync(layout: null);

        vm.SelectSource(LanguageCatalog.KeyboardLayoutCode);

        Assert.Equal("Layout detection unavailable — auto-detecting instead", vm.SourceSubHint);
    }

    [Fact]
    public async Task SelectSource_SameCode_IsNoOp()
    {
        var (vm, _, _) = await CreateAsync();
        vm.SelectSource("ru");
        var raised = 0;
        vm.RelationshipChanged += (_, _) => raised++;

        vm.SelectSource("RU");

        Assert.Equal(0, raised);
        Assert.Equal(["ru"], vm.SourceRecent);
    }

    // ----- Target selection ---------------------------------------------------

    [Fact]
    public async Task SelectTarget_RealLanguage_EnablesTranslation()
    {
        var (vm, settings, _) = await CreateAsync(SpeechEngine.Gemma4);

        vm.SelectTarget("fr");

        Assert.True(vm.TranslationEnabled);
        Assert.Equal("fr", vm.TargetCode);
        Assert.Equal(["fr"], vm.TargetRecent);
        Assert.Equal(true.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SelectTarget_OffRow_DisablesTranslation_KeepsRestingTarget()
    {
        var (vm, settings, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectTarget("fr");

        vm.SelectTarget(LanguageCatalog.NoTranslationCode);

        Assert.False(vm.TranslationEnabled);
        Assert.Equal("fr", vm.TargetCode);
        Assert.Equal(false.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled, TestContext.Current.CancellationToken));
    }

    // ----- Engine switch fallbacks (spec §8) -----------------------------------

    [Fact]
    public async Task SetEngine_UnsupportedSource_FallsBackToKeyboard_WithToast()
    {
        // Irish is in the full (Gemma) catalog but not in Whisper's fixed set.
        var (vm, settings, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectSource("ga");

        vm.SetEngine(SpeechEngine.Whisper);

        Assert.Equal(LanguageCatalog.KeyboardLayoutCode, vm.SourceCode);
        Assert.Equal("Irish isn't a source in Whisper. Using your keyboard layout.", vm.ToastMessage);
        Assert.Equal(LanguageCatalog.KeyboardLayoutCode,
            await settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetEngine_ToggleForm_ForcesSingleTarget_WithToast()
    {
        var (vm, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectTarget("fr");

        vm.SetEngine(SpeechEngine.Whisper);

        Assert.Equal("en", vm.TargetCode);
        Assert.True(vm.TranslationEnabled);
        Assert.Equal("French isn't available in Whisper. Translation set to English.", vm.ToastMessage);
    }

    [Fact]
    public async Task SetEngine_ToggleForm_RestingTargetResetsSilently()
    {
        // Translation is off, so the forced reset is invisible — no toast.
        var (vm, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectTarget("fr");
        vm.ToggleTranslation(); // off, resting target stays fr

        vm.SetEngine(SpeechEngine.Whisper);

        Assert.Equal("en", vm.TargetCode);
        Assert.False(vm.TranslationEnabled);
        Assert.Null(vm.ToastMessage);
    }

    [Fact]
    public async Task SetEngine_NoneForm_TurnsTranslationOff_WithToast()
    {
        var (vm, settings, _) = await CreateAsync();
        vm.ToggleTranslation();

        vm.SetEngine(TranscribeOnlyEngine);

        Assert.False(vm.TranslationEnabled);
        Assert.Equal(TranslationForm.None, vm.TargetForm);
        Assert.Equal(ConnectorState.Locked, vm.Connector);
        Assert.Equal("999 can't translate — output now matches your spoken language.", vm.ToastMessage);
        Assert.Equal(false.ToString(), await settings.GetAsync<string>(SettingsKeys.TranslationEnabled, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SetEngine_SupportedSelections_SurviveUnchanged_NoToast()
    {
        var (vm, _, _) = await CreateAsync();
        vm.SelectSource("ru");
        vm.ToggleTranslation(); // whisper → en

        vm.SetEngine(SpeechEngine.Gemma4);

        Assert.Equal("ru", vm.SourceCode);
        Assert.Equal("en", vm.TargetCode);
        Assert.True(vm.TranslationEnabled);
        Assert.Null(vm.ToastMessage);
        Assert.Equal(TranslationForm.Full, vm.TargetForm);
    }

    [Fact]
    public async Task SetEngine_NewToast_ReplacesPrevious()
    {
        var (vm, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectSource("ga");
        vm.SelectTarget("fr");

        vm.SetEngine(SpeechEngine.Whisper);

        // Both fallbacks fire; the target one is shown last.
        Assert.Equal("French isn't available in Whisper. Translation set to English.", vm.ToastMessage);
    }

    // ----- ADR-033 paused note --------------------------------------------------

    [Fact]
    public async Task PausedNote_Shown_WhenWhisperModelCannotTranslate()
    {
        var (vm, _, _) = await CreateAsync();
        vm.ToggleTranslation();

        vm.SetWhisperModel(WhisperModelType.LargeV3Turbo);

        Assert.True(vm.ShowTranslationPausedNote);

        vm.SetWhisperModel(WhisperModelType.Medium);
        Assert.False(vm.ShowTranslationPausedNote);
    }

    [Fact]
    public async Task PausedNote_NeverShown_OnFullFormEngines()
    {
        var (vm, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectTarget("fr");

        vm.SetWhisperModel(WhisperModelType.LargeV3Turbo);

        Assert.False(vm.ShowTranslationPausedNote);
    }

    // ----- Summary line (spec §7 derived rendering) ------------------------------

    [Fact]
    public async Task Summary_AutoSource_TranslationOff()
    {
        var (vm, _, _) = await CreateAsync();

        Assert.Equal("You speak any language → Parlotype types any language (no translation).", vm.SummaryText);
    }

    [Fact]
    public async Task Summary_ExplicitSource_TranslationOn()
    {
        var (vm, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectSource("ru");
        vm.SelectTarget("fr");

        Assert.Equal("You speak Russian → Parlotype types French.", vm.SummaryText);
    }

    [Fact]
    public async Task Summary_KeyboardSource_UsesDetectedLanguageName()
    {
        var (vm, _, _) = await CreateAsync(
            layout: new KeyboardLayoutInfo("ru", "Russian (Russia)"));
        vm.SelectSource(LanguageCatalog.KeyboardLayoutCode);

        Assert.Equal("You speak Russian → Parlotype types Russian (no translation).", vm.SummaryText);
    }

    [Fact]
    public async Task TargetDisplayLabel_ShowsSameAsSource_WhenTranslationOff()
    {
        var (vm, _, _) = await CreateAsync();

        Assert.False(vm.TranslationEnabled);
        Assert.Equal("Same as source", vm.TargetDisplayLabel);
    }

    [Fact]
    public async Task TargetDisplayLabel_RevertsToSameAsSource_WhenTranslationDisabled()
    {
        // Repro: Gemma, source French, target Russian, then disable translation.
        var (vm, _, _) = await CreateAsync(SpeechEngine.Gemma4);
        vm.SelectSource("fr");
        vm.SelectTarget("ru");
        Assert.Equal(LanguageCatalog.GetDisplayLabel("ru"), vm.TargetDisplayLabel);

        vm.ToggleTranslation();

        Assert.False(vm.TranslationEnabled);
        Assert.Equal("Same as source", vm.TargetDisplayLabel);
    }

    // ----- Keyboard layout refresh ----------------------------------------------

    [Fact]
    public async Task RefreshKeyboardLayout_PicksUpLayoutChange()
    {
        var (vm, _, keyboard) = await CreateAsync(
            layout: new KeyboardLayoutInfo("en", "English (United States)"));

        keyboard.Result = new KeyboardLayoutInfo("de", "German (Germany)");
        vm.RefreshKeyboardLayout();

        Assert.Equal("de", vm.DetectedKeyboardLayout?.LanguageCode);
    }

    // ----- Live polling (Alt+Shift keeps the detected layout current) -----------

    private static async Task<LanguageRelationshipViewModel> CreateWithSourceAsync(
        string sourceCode, KeyboardLayoutInfo? layout = null)
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, sourceCode, TestContext.Current.CancellationToken);
        var vm = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService { Result = layout });
        await vm.InitializeAsync(TestContext.Current.CancellationToken);
        return vm;
    }

    [AvaloniaFact]
    public async Task LivePolling_InactiveForNonKeyboardSource()
    {
        var (vm, _, _) = await CreateAsync();

        vm.BeginLivePolling();

        Assert.False(vm.IsLayoutPollActive);
    }

    [AvaloniaFact]
    public async Task LivePolling_ActivatesAndStopsForKeyboardSource()
    {
        var vm = await CreateWithSourceAsync(LanguageCatalog.KeyboardLayoutCode);

        vm.BeginLivePolling();
        Assert.True(vm.IsLayoutPollActive);

        vm.EndLivePolling();
        Assert.False(vm.IsLayoutPollActive);
    }

    [AvaloniaFact]
    public async Task LivePolling_RefcountBalancesMultipleSurfaces()
    {
        var vm = await CreateWithSourceAsync(LanguageCatalog.KeyboardLayoutCode);

        vm.BeginLivePolling();
        vm.BeginLivePolling();
        vm.EndLivePolling();

        Assert.True(vm.IsLayoutPollActive); // one surface still visible

        vm.EndLivePolling();
        Assert.False(vm.IsLayoutPollActive);
    }

    [AvaloniaFact]
    public async Task LivePolling_FollowsSourceCodeChanges()
    {
        var (vm, _, _) = await CreateAsync();

        vm.BeginLivePolling();
        Assert.False(vm.IsLayoutPollActive); // auto source — nothing to detect

        vm.SelectSource(LanguageCatalog.KeyboardLayoutCode);
        Assert.True(vm.IsLayoutPollActive);

        vm.SelectSource("en");
        Assert.False(vm.IsLayoutPollActive);
    }
}
