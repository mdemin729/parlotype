using Parlotype.Core.Audio;
using Parlotype.Desktop.V2.Tests.Mocks;
using Parlotype.Desktop.V2.ViewModels;
using Parlotype.Desktop.V2.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.V2.Tests;

public class SettingsWindowViewModelTests
{
    private static SettingsWindowViewModel BuildViewModel()
    {
        var settings = new MockSettingsService();
        var enumerator = new MockMicrophoneEnumerator(new MicrophoneInfo("m1", "Mic 1", true));

        var mic = new MicrophoneSettingsViewModel(enumerator, settings);
        var model = new WhisperModelSettingsViewModel(settings);
        var hotkey = new HotkeySettingsViewModel(hotkeyService: null, settings);
        var theme = new ThemeSettingsViewModel(settings);

        return new SettingsWindowViewModel(mic, model, hotkey, theme);
    }

    [Fact]
    public void Sections_ContainsAllFourInOrder()
    {
        var vm = BuildViewModel();

        Assert.Collection(vm.Sections,
            s => Assert.Equal("Microphone", s.Title),
            s => Assert.Equal("Whisper Model", s.Title),
            s => Assert.Equal("Hotkey", s.Title),
            s => Assert.Equal("Theme", s.Title));
    }

    [Fact]
    public void DefaultSelectedSection_IsMicrophone()
    {
        var vm = BuildViewModel();

        Assert.NotNull(vm.SelectedSection);
        Assert.Equal("Microphone", vm.SelectedSection!.Title);
    }

    [Fact]
    public void SelectedSection_CanBeChanged()
    {
        var vm = BuildViewModel();

        vm.SelectedSection = vm.Hotkey;

        Assert.Same(vm.Hotkey, vm.SelectedSection);
    }
}
