using Parlotype.Core.Audio;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class SettingsWindowViewModelTests
{
    private static SettingsWindowViewModel BuildViewModel()
    {
        var settings = new MockSettingsService();
        var enumerator = new MockMicrophoneEnumerator(new MicrophoneInfo("m1", "Mic 1", true));
        var nvidia = new MockNvidiaEnvironmentProvider();
        var vulkan = new MockVulkanEnvironmentProvider();

        var engine = new SpeechEngineSettingsViewModel(settings);
        var mic = new MicrophoneSettingsViewModel(enumerator, settings);
        var model = new WhisperModelSettingsViewModel(settings);
        var runtime = new RuntimeSettingsViewModel(settings, nvidia, vulkan);
        var llamaCpp = new LlamaCppSettingsViewModel(settings);
        var hotkey = new HotkeySettingsViewModel(hotkeyService: null, settings);
        var speech = new SpeechSettingsViewModel(settings);
        var theme = new ThemeSettingsViewModel(settings);

        return new SettingsWindowViewModel(engine, mic, model, runtime, llamaCpp, hotkey, speech, theme);
    }

    [Fact]
    public void Sections_ContainsAllEightInOrder()
    {
        var vm = BuildViewModel();

        Assert.Collection(vm.Sections,
            s => Assert.Equal("Speech Engine", s.Title),
            s => Assert.Equal("Microphone", s.Title),
            s => Assert.Equal("Whisper Model", s.Title),
            s => Assert.Equal("Runtime", s.Title),
            s => Assert.Equal("llama.cpp", s.Title),
            s => Assert.Equal("Hotkey", s.Title),
            s => Assert.Equal("Speech", s.Title),
            s => Assert.Equal("Theme", s.Title));
    }

    [Fact]
    public void DefaultSelectedSection_IsSpeechEngine()
    {
        var vm = BuildViewModel();

        Assert.NotNull(vm.SelectedSection);
        Assert.Equal("Speech Engine", vm.SelectedSection!.Title);
    }

    [Fact]
    public void SelectedSection_CanBeChanged()
    {
        var vm = BuildViewModel();

        vm.SelectedSection = vm.Hotkey;

        Assert.Same(vm.Hotkey, vm.SelectedSection);
    }

    [Fact]
    public async Task SelectingLlamaCppSection_TriggersServerProbe()
    {
        var vm = BuildViewModel();
        Assert.Equal("Not probed", vm.LlamaCpp.StatusText);

        vm.SelectedSection = vm.LlamaCpp;

        var executionTask = vm.LlamaCpp.RefreshServerInfoCommand.ExecutionTask;
        Assert.NotNull(executionTask);
        await executionTask!;

        Assert.NotEqual("Not probed", vm.LlamaCpp.StatusText);
        Assert.False(vm.LlamaCpp.IsRefreshing);
    }
}
