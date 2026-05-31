using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class SettingsWindowViewModelTests
{
    private static SettingsWindowViewModel BuildViewModel(MockSettingsService? settings = null)
    {
        settings ??= new MockSettingsService();
        var enumerator = new MockMicrophoneEnumerator(new MicrophoneInfo("m1", "Mic 1", true));
        var nvidia = new MockNvidiaEnvironmentProvider();
        var vulkan = new MockVulkanEnvironmentProvider();

        var engine = new SpeechEngineSettingsViewModel(settings);
        var mic = new MicrophoneSettingsViewModel(enumerator, settings);
        var silence = new SilenceTimeoutSettingsViewModel(settings);
        var model = new WhisperModelSettingsViewModel(settings);
        var runtime = new RuntimeSettingsViewModel(settings, nvidia, vulkan);
        var whisperOutput = new WhisperOutputSettingsViewModel(settings);
        var language = new LanguageSelectionSettingsViewModel(settings);
        var gemma4Model = new Gemma4ModelSettingsViewModel(settings);
        var prompts = new PromptSettingsViewModel(new MockPromptTemplateRegistry());
        var llamaCpp = new LlamaCppSettingsViewModel(settings);
        var hotkey = new HotkeySettingsViewModel(hotkeyService: null, settings);
        var theme = new ThemeSettingsViewModel(settings);

        return new SettingsWindowViewModel(
            engine, mic, silence, model, runtime, whisperOutput, language, gemma4Model, prompts, llamaCpp, hotkey, theme);
    }

    [Fact]
    public void NavItems_WithWhisperActive_AreOrderedByCategoryWithHeaders()
    {
        var vm = BuildViewModel();

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
            n => AssertSection(n, "Hotkey"),
            n => AssertHeader(n, "Appearance"),
            n => AssertSection(n, "Theme"));
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
            n => AssertSection(n, "Hotkey"),
            n => AssertHeader(n, "Appearance"),
            n => AssertSection(n, "Theme"));
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

        var runtimeRow = vm.NavItems.Single(n => n.Section is RuntimeSettingsViewModel);
        vm.SelectedNavItem = runtimeRow;
        Assert.Same(vm.Runtime, vm.SelectedSection);

        vm.SpeechEngine.SelectEngineCommand.Execute(SpeechEngine.Gemma4);

        Assert.NotSame(vm.Runtime, vm.SelectedSection);
        Assert.NotNull(vm.SelectedSection);
        Assert.False(vm.SelectedNavItem!.IsHeader);
    }

    [Fact]
    public void SelectingNonTranslatingModel_DisablesTranslateToggle()
    {
        var vm = BuildViewModel();

        // Base (default) supports translation.
        Assert.True(vm.WhisperOutput.CanTranslate);

        // Large v3 Turbo does not — changing the model must flow through the
        // PropertyChanged wiring and disable the translate toggle.
        vm.WhisperModel.SelectModelCommand.Execute(WhisperModelType.LargeV3Turbo);
        Assert.False(vm.WhisperOutput.CanTranslate);

        // Switching back to a translation-capable model re-enables it.
        vm.WhisperModel.SelectModelCommand.Execute(WhisperModelType.Medium);
        Assert.True(vm.WhisperOutput.CanTranslate);
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
