using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Platform.Startup;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class SettingsWindowViewModelTests
{
    private static SettingsWindowViewModel BuildViewModel(MockSettingsService? settings = null)
    {
        settings ??= new MockSettingsService();
        var enumerator = new MockMicrophoneEnumerator(new MicrophoneInfo("m1", "Mic 1", true));
        var vulkan = new MockVulkanEnvironmentProvider();

        var engine = new SpeechEngineSettingsViewModel(settings);
        var mic = new MicrophoneSettingsViewModel(enumerator, settings);
        var silence = new SilenceTimeoutSettingsViewModel(settings);
        var model = new WhisperModelSettingsViewModel(settings);
        var runtime = new RuntimeSettingsViewModel(settings, vulkan);
        var whisperOutput = new WhisperOutputSettingsViewModel(settings);
        var language = new LanguageSelectionSettingsViewModel(
            new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService()));
        var gemma4Model = new Gemma4ModelSettingsViewModel(settings);
        var parakeetModel = new ParakeetModelSettingsViewModel(settings);
        var cloudProviders = new CloudProviderSettingsViewModel(settings, new MockSecretStore());
        var prompts = new PromptSettingsViewModel(new MockPromptTemplateRegistry());
        var llamaCpp = new LlamaCppSettingsViewModel(settings);
        var hotkey = new HotkeySettingsViewModel(hotkeyService: null, settings);
        var theme = new ThemeSettingsViewModel(settings);
        var startup = new StartupSettingsViewModel(new LaunchAtLoginCoordinator(
            settings,
            new MockLaunchAtLoginService(),
            NullLogger<LaunchAtLoginCoordinator>.Instance));
        var updates = new UpdateSettingsViewModel(settings, new MockUpdateService());
        var data = new DataSettingsViewModel(settings);
        var help = new HelpSettingsViewModel(new MockOnboardingService(), hotkeyService: null);

        return new SettingsWindowViewModel(
            engine, mic, silence, model, runtime, whisperOutput, language, gemma4Model, parakeetModel,
            cloudProviders, prompts, llamaCpp, hotkey, theme, startup, updates, data, help);
    }

    [Fact]
    public void NavItems_WithWhisperActive_AreOrderedByCategoryWithHeaders()
    {
        var vm = BuildViewModel();
        // Parakeet is the default engine — Whisper must be selected explicitly.
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Whisper);

        Assert.Collection(vm.NavItems,
            n => AssertHeader(n, "Audio"),
            n => AssertSection(n, "Microphone"),
            n => AssertSection(n, "Silence timeout"),
            n => AssertHeader(n, "Speech engine"),
            n => AssertSection(n, "Engine"),
            n => AssertSection(n, "Language"),
            n => AssertSection(n, "Whisper model"),
            n => AssertSection(n, "Whisper runtime"),
            n => AssertSection(n, "Whisper output"),
            n => AssertHeader(n, "Input"),
            n => AssertSection(n, "Hotkeys"),
            n => AssertHeader(n, "Appearance"),
            n => AssertSection(n, "Theme"),
            n => AssertHeader(n, "Application"),
            n => AssertSection(n, "Startup"),
            n => AssertSection(n, "Updates"),
            n => AssertSection(n, "Data"),
            n => AssertSection(n, "Help"));
    }

    [Fact]
    public async Task NavItems_WithGemma4Active_HideWhisperRows_AndShowLlamaCpp()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);

        var vm = BuildViewModel(settings);
        // Allow the engine VM's InitializeAsync to run; it's fire-and-forget,
        // so prod a deterministic state by selecting Gemma 4 directly.
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Gemma4);

        Assert.Collection(vm.NavItems,
            n => AssertHeader(n, "Audio"),
            n => AssertSection(n, "Microphone"),
            n => AssertSection(n, "Silence timeout"),
            n => AssertHeader(n, "Speech engine"),
            n => AssertSection(n, "Engine"),
            n => AssertSection(n, "Language"),
            n => AssertSection(n, "Gemma 4 model"),
            n => AssertSection(n, "Prompts"),
            n => AssertSection(n, "llama.cpp server"),
            n => AssertHeader(n, "Input"),
            n => AssertSection(n, "Hotkeys"),
            n => AssertHeader(n, "Appearance"),
            n => AssertSection(n, "Theme"),
            n => AssertHeader(n, "Application"),
            n => AssertSection(n, "Startup"),
            n => AssertSection(n, "Updates"),
            n => AssertSection(n, "Data"),
            n => AssertSection(n, "Help"));
    }

    [Fact]
    public void NavItems_WithParakeetActive_HideWhisperGemmaAndLanguageRows_AndShowParakeetModel()
    {
        // Parakeet is the default — no explicit selection needed. The Language
        // page is hidden too: the engine auto-detects and cannot translate, so
        // it offers no language choice at all.
        var vm = BuildViewModel();

        Assert.Collection(vm.NavItems,
            n => AssertHeader(n, "Audio"),
            n => AssertSection(n, "Microphone"),
            n => AssertSection(n, "Silence timeout"),
            n => AssertHeader(n, "Speech engine"),
            n => AssertSection(n, "Engine"),
            n => AssertSection(n, "Parakeet model"),
            n => AssertHeader(n, "Input"),
            n => AssertSection(n, "Hotkeys"),
            n => AssertHeader(n, "Appearance"),
            n => AssertSection(n, "Theme"),
            n => AssertHeader(n, "Application"),
            n => AssertSection(n, "Startup"),
            n => AssertSection(n, "Updates"),
            n => AssertSection(n, "Data"),
            n => AssertSection(n, "Help"));
    }

    [Fact]
    public void NavItems_WithOpenAiCompatActive_ShowCloudProviders()
    {
        var vm = BuildViewModel();
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.OpenAiCompatible);

        // Cloud engines always auto-detect and cannot translate — like Parakeet
        // they have no language choices (ADR-043), so the Language page hides;
        // the Cloud providers section shows instead.
        Assert.Collection(vm.NavItems,
            n => AssertHeader(n, "Audio"),
            n => AssertSection(n, "Microphone"),
            n => AssertSection(n, "Silence timeout"),
            n => AssertHeader(n, "Speech engine"),
            n => AssertSection(n, "Engine"),
            n => AssertSection(n, "Cloud providers"),
            n => AssertHeader(n, "Input"),
            n => AssertSection(n, "Hotkeys"),
            n => AssertHeader(n, "Appearance"),
            n => AssertSection(n, "Theme"),
            n => AssertHeader(n, "Application"),
            n => AssertSection(n, "Startup"),
            n => AssertSection(n, "Updates"),
            n => AssertSection(n, "Data"),
            n => AssertSection(n, "Help"));
    }

    [Fact]
    public void NavItems_WithXaiGrokActive_ShowCloudProviders()
    {
        var vm = BuildViewModel();
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.XaiGrok);

        var cloudRow = vm.NavItems.Single(n => !n.IsHeader && n.Section is CloudProviderSettingsViewModel);
        Assert.Equal("Cloud providers", cloudRow.Label);
    }

    [Fact]
    public void NavItems_WithParakeetActive_HideCloudProviders()
    {
        var vm = BuildViewModel();

        Assert.DoesNotContain(vm.NavItems, n => n.Section is CloudProviderSettingsViewModel);
    }

    [Fact]
    public void DefaultSelectedNavItem_IsFirstSection()
    {
        var vm = BuildViewModel();

        Assert.NotNull(vm.SelectedNavItem);
        Assert.False(vm.SelectedNavItem!.IsHeader);
        Assert.Equal("Microphone", vm.SelectedNavItem.Label);
        Assert.Same(vm.Microphone, vm.SelectedSection);
    }

    [Fact]
    public void SelectedNavItem_CanBeChanged()
    {
        var vm = BuildViewModel();

        var hotkeyRow = vm.NavItems.Single(n => !n.IsHeader && n.Section is HotkeySettingsViewModel);
        vm.SelectedNavItem = hotkeyRow;

        Assert.Same(vm.Hotkey, vm.SelectedSection);
    }

    [Fact]
    public async Task SelectingLlamaCppSection_TriggersServerProbe()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);
        var vm = BuildViewModel(settings);
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Gemma4);

        Assert.Equal("Not probed", vm.LlamaCpp.StatusText);

        var llamaRow = vm.NavItems.Single(n => !n.IsHeader && n.Section is LlamaCppSettingsViewModel);
        vm.SelectedNavItem = llamaRow;

        var executionTask = vm.LlamaCpp.RefreshServerInfoCommand.ExecutionTask;
        Assert.NotNull(executionTask);
        await executionTask!;

        Assert.NotEqual("Not probed", vm.LlamaCpp.StatusText);
        Assert.False(vm.LlamaCpp.IsRefreshing);
    }

    [Fact]
    public void SwitchingEngine_PreservesSelection_WhenStillVisible()
    {
        var vm = BuildViewModel();

        var hotkeyRow = vm.NavItems.Single(n => n.Section is HotkeySettingsViewModel);
        vm.SelectedNavItem = hotkeyRow;
        Assert.Same(vm.Hotkey, vm.SelectedSection);

        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Gemma4);

        Assert.Same(vm.Hotkey, vm.SelectedSection);
    }

    [Fact]
    public void SwitchingEngine_FallsBackToFirstSection_WhenSelectionIsHidden()
    {
        var vm = BuildViewModel();
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Whisper);

        var runtimeRow = vm.NavItems.Single(n => n.Section is RuntimeSettingsViewModel);
        vm.SelectedNavItem = runtimeRow;
        Assert.Same(vm.Runtime, vm.SelectedSection);

        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Gemma4);

        Assert.NotSame(vm.Runtime, vm.SelectedSection);
        Assert.NotNull(vm.SelectedSection);
        Assert.False(vm.SelectedNavItem!.IsHeader);
    }

    [Fact]
    public void SelectingNonTranslatingModel_FlipsPausedNoteOnLanguagePage()
    {
        var vm = BuildViewModel();
        // The paused note is a Whisper concern; Parakeet (default) hides the page.
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Whisper);

        // Translation has to be on for the paused note to be reachable.
        vm.Language.ToggleTranslationCommand.Execute(null);

        // Base (default) supports translation — note stays hidden.
        Assert.False(vm.Language.Relationship.IsTranslationPaused);

        // Large v3 Turbo does not — the model change must propagate through the
        // SettingsWindowViewModel wiring and surface the paused note.
        vm.WhisperModel.SelectModelCommand.Execute(WhisperModelType.LargeV3Turbo);
        Assert.True(vm.Language.Relationship.IsTranslationPaused);

        // Switching back to a translation-capable model hides the note again.
        vm.WhisperModel.SelectModelCommand.Execute(WhisperModelType.Medium);
        Assert.False(vm.Language.Relationship.IsTranslationPaused);
    }

    [Fact]
    public void NavigateTo_Language_SelectsLanguageSection_OverridingPriorSelection()
    {
        var vm = BuildViewModel();
        // The Language page only exists for engines with language choices.
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Whisper);

        // Simulate the window having been left on another page.
        var hotkeyRow = vm.NavItems.Single(n => n.Section is HotkeySettingsViewModel);
        vm.SelectedNavItem = hotkeyRow;
        Assert.Same(vm.Hotkey, vm.SelectedSection);

        vm.NavigateTo(SettingsSection.Language);

        Assert.Same(vm.Language, vm.SelectedSection);
        Assert.Equal("Language", vm.SelectedNavItem!.Label);
    }

    [Fact]
    public void NavigateTo_CloudProviders_SelectsCloudSection_WhenCloudEngineActive()
    {
        var vm = BuildViewModel();
        // The Cloud providers page only exists while a cloud engine is selected —
        // exactly the state in the not-configured error flow that deep-links here.
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.XaiGrok);

        vm.NavigateTo(SettingsSection.CloudProviders);

        Assert.Same(vm.CloudProviders, vm.SelectedSection);
        Assert.Equal("Cloud providers", vm.SelectedNavItem!.Label);
    }

    [Fact]
    public void NavigateTo_CloudProviders_NoOp_WhenLocalEngineActive()
    {
        var vm = BuildViewModel();
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Whisper);
        var before = vm.SelectedNavItem;

        vm.NavigateTo(SettingsSection.CloudProviders);

        Assert.Same(before, vm.SelectedNavItem);
    }

    [Fact]
    public void NavigateTo_Engine_SelectsEngineSection()
    {
        var vm = BuildViewModel();
        vm.SelectedNavItem = vm.NavItems.Single(n => n.Section is HotkeySettingsViewModel);

        vm.NavigateTo(SettingsSection.Engine);

        Assert.Same(vm.SpeechEngine, vm.SelectedSection);
    }

    [Fact]
    public void NavigateTo_EngineModel_SelectsTheActiveEnginesModelPage()
    {
        var vm = BuildViewModel();

        // Parakeet (default) → Parakeet model page.
        vm.NavigateTo(SettingsSection.EngineModel);
        Assert.Same(vm.ParakeetModel, vm.SelectedSection);

        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Whisper);
        vm.NavigateTo(SettingsSection.EngineModel);
        Assert.Same(vm.WhisperModel, vm.SelectedSection);

        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Gemma4);
        vm.NavigateTo(SettingsSection.EngineModel);
        Assert.Same(vm.Gemma4Model, vm.SelectedSection);
    }

    [Fact]
    public void NavigateTo_EngineModel_FallsBackToEngine_ForCloudEngines()
    {
        var vm = BuildViewModel();
        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.XaiGrok);

        // Cloud engines have no local model page (ADR-056).
        vm.NavigateTo(SettingsSection.EngineModel);

        Assert.Same(vm.SpeechEngine, vm.SelectedSection);
    }

    [Fact]
    public void NavigateTo_Help_SelectsHelpSection()
    {
        var vm = BuildViewModel();

        vm.NavigateTo(SettingsSection.Help);

        Assert.Same(vm.Help, vm.SelectedSection);
        Assert.Equal("Help", vm.SelectedNavItem!.Label);
    }

    private static void AssertHeader(SettingsNavItem item, string label)
    {
        Assert.True(item.IsHeader, $"Expected header '{label}' but row was a section.");
        Assert.Equal(label, item.Label);
        Assert.Null(item.Section);
    }

    private static void AssertSection(SettingsNavItem item, string label)
    {
        Assert.False(item.IsHeader, $"Expected section '{label}' but row was a header.");
        Assert.Equal(label, item.Label);
        Assert.NotNull(item.Section);
    }
}
