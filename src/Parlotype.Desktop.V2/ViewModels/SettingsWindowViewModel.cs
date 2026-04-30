using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Desktop.V2.ViewModels.Settings;

namespace Parlotype.Desktop.V2.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public ObservableCollection<SettingsSectionViewModelBase> Sections { get; } = [];

    [ObservableProperty]
    private SettingsSectionViewModelBase? _selectedSection;

    public MicrophoneSettingsViewModel Microphone { get; }
    public WhisperModelSettingsViewModel WhisperModel { get; }
    public HotkeySettingsViewModel Hotkey { get; }
    public ThemeSettingsViewModel Theme { get; }

    public SettingsWindowViewModel(
        MicrophoneSettingsViewModel microphone,
        WhisperModelSettingsViewModel whisperModel,
        HotkeySettingsViewModel hotkey,
        ThemeSettingsViewModel theme)
    {
        Microphone = microphone;
        WhisperModel = whisperModel;
        Hotkey = hotkey;
        Theme = theme;

        Sections.Add(microphone);
        Sections.Add(whisperModel);
        Sections.Add(hotkey);
        Sections.Add(theme);

        SelectedSection = Sections[0];
    }
}
