using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Desktop.ViewModels.Settings;

namespace Parlotype.Desktop.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    public ObservableCollection<SettingsSectionViewModelBase> Sections { get; } = [];

    [ObservableProperty]
    private SettingsSectionViewModelBase? _selectedSection;

    public SpeechEngineSettingsViewModel SpeechEngine { get; }
    public MicrophoneSettingsViewModel Microphone { get; }
    public WhisperModelSettingsViewModel WhisperModel { get; }
    public RuntimeSettingsViewModel Runtime { get; }
    public LlamaCppSettingsViewModel LlamaCpp { get; }
    public HotkeySettingsViewModel Hotkey { get; }
    public SpeechSettingsViewModel Speech { get; }
    public ThemeSettingsViewModel Theme { get; }

    public SettingsWindowViewModel(
        SpeechEngineSettingsViewModel speechEngine,
        MicrophoneSettingsViewModel microphone,
        WhisperModelSettingsViewModel whisperModel,
        RuntimeSettingsViewModel runtime,
        LlamaCppSettingsViewModel llamaCpp,
        HotkeySettingsViewModel hotkey,
        SpeechSettingsViewModel speech,
        ThemeSettingsViewModel theme)
    {
        SpeechEngine = speechEngine;
        Microphone = microphone;
        WhisperModel = whisperModel;
        Runtime = runtime;
        LlamaCpp = llamaCpp;
        Hotkey = hotkey;
        Speech = speech;
        Theme = theme;

        Sections.Add(speechEngine);
        Sections.Add(microphone);
        Sections.Add(whisperModel);
        Sections.Add(runtime);
        Sections.Add(llamaCpp);
        Sections.Add(hotkey);
        Sections.Add(speech);
        Sections.Add(theme);

        SelectedSection = Sections[0];
    }

    partial void OnSelectedSectionChanged(SettingsSectionViewModelBase? value)
    {
        if (value is LlamaCppSettingsViewModel llamaCpp
            && llamaCpp.RefreshServerInfoCommand.CanExecute(null))
        {
            llamaCpp.RefreshServerInfoCommand.Execute(null);
        }
    }
}
