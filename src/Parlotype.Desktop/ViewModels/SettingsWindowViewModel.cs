using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Desktop.ViewModels.Settings;

namespace Parlotype.Desktop.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public ObservableCollection<SettingsSectionViewModelBase> Sections { get; } = [];

    [ObservableProperty]
    private SettingsSectionViewModelBase? _selectedSection;

    public MicrophoneSettingsViewModel Microphone { get; }
    public WhisperModelSettingsViewModel WhisperModel { get; }
    public RuntimeSettingsViewModel Runtime { get; }
    public HotkeySettingsViewModel Hotkey { get; }
    public SpeechSettingsViewModel Speech { get; }
    public ThemeSettingsViewModel Theme { get; }

    public SettingsWindowViewModel(
        MicrophoneSettingsViewModel microphone,
        WhisperModelSettingsViewModel whisperModel,
        RuntimeSettingsViewModel runtime,
        HotkeySettingsViewModel hotkey,
        SpeechSettingsViewModel speech,
        ThemeSettingsViewModel theme)
    {
        Microphone = microphone;
        WhisperModel = whisperModel;
        Runtime = runtime;
        Hotkey = hotkey;
        Speech = speech;
        Theme = theme;

        Sections.Add(microphone);
        Sections.Add(whisperModel);
        Sections.Add(runtime);
        Sections.Add(hotkey);
        Sections.Add(speech);
        Sections.Add(theme);

        SelectedSection = Sections[0];
    }
}
