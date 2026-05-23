using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;
using Parlotype.Desktop.ViewModels.Settings;

namespace Parlotype.Desktop.ViewModels;

public partial class SettingsWindowViewModel : ViewModelBase
{
    private readonly SettingsSectionViewModelBase[] _allSections;

    /// <summary>
    /// Flat projection of nav rows (group headers + visible sections) bound to
    /// the left ListBox. Recomputed when the active speech engine changes.
    /// </summary>
    public ObservableCollection<SettingsNavItem> NavItems { get; } = [];

    [ObservableProperty]
    private SettingsNavItem? _selectedNavItem;

    public SettingsSectionViewModelBase? SelectedSection =>
        SelectedNavItem?.Section;

    public SpeechEngineSettingsViewModel SpeechEngine { get; }
    public MicrophoneSettingsViewModel Microphone { get; }
    public SilenceTimeoutSettingsViewModel SilenceTimeout { get; }
    public WhisperModelSettingsViewModel WhisperModel { get; }
    public RuntimeSettingsViewModel Runtime { get; }
    public WhisperOutputSettingsViewModel WhisperOutput { get; }
    public Gemma4ModelSettingsViewModel Gemma4Model { get; }
    public PromptSettingsViewModel Prompts { get; }
    public LlamaCppSettingsViewModel LlamaCpp { get; }
    public HotkeySettingsViewModel Hotkey { get; }
    public ThemeSettingsViewModel Theme { get; }

    public SettingsWindowViewModel(
        SpeechEngineSettingsViewModel speechEngine,
        MicrophoneSettingsViewModel microphone,
        SilenceTimeoutSettingsViewModel silenceTimeout,
        WhisperModelSettingsViewModel whisperModel,
        RuntimeSettingsViewModel runtime,
        WhisperOutputSettingsViewModel whisperOutput,
        Gemma4ModelSettingsViewModel gemma4Model,
        PromptSettingsViewModel prompts,
        LlamaCppSettingsViewModel llamaCpp,
        HotkeySettingsViewModel hotkey,
        ThemeSettingsViewModel theme)
    {
        SpeechEngine = speechEngine;
        Microphone = microphone;
        SilenceTimeout = silenceTimeout;
        WhisperModel = whisperModel;
        Runtime = runtime;
        WhisperOutput = whisperOutput;
        Gemma4Model = gemma4Model;
        Prompts = prompts;
        LlamaCpp = llamaCpp;
        Hotkey = hotkey;
        Theme = theme;

        // Fixed authoring order — engine-filtered rows are dropped per active
        // engine, but the order of those that survive is preserved.
        _allSections =
        [
            microphone,
            silenceTimeout,
            speechEngine,
            whisperModel,
            runtime,
            whisperOutput,
            gemma4Model,
            prompts,
            llamaCpp,
            hotkey,
            theme,
        ];

        speechEngine.PropertyChanged += OnSpeechEnginePropertyChanged;

        RebuildNavItems();
        SelectedNavItem = NavItems.FirstOrDefault(n => !n.IsHeader);
    }

    private void OnSpeechEnginePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpeechEngineSettingsViewModel.SelectedEngine))
            RebuildNavItems();
    }

    private void RebuildNavItems()
    {
        var activeEngine = SpeechEngine.SelectedEngine;
        var previousSection = SelectedNavItem?.Section;

        var visible = _allSections
            .Where(s => s.RestrictToEngine is null || s.RestrictToEngine == activeEngine)
            .ToList();

        NavItems.Clear();
        foreach (var category in Enum.GetValues<SettingsCategory>())
        {
            var sectionsInCategory = visible.Where(s => s.Category == category).ToList();
            if (sectionsInCategory.Count == 0)
                continue;

            NavItems.Add(SettingsNavItem.Header(category.GetDisplayName()));
            foreach (var section in sectionsInCategory)
                NavItems.Add(SettingsNavItem.ForSection(section));
        }

        // Preserve selection if the previously-selected section is still
        // visible; otherwise fall back to the first non-header row.
        if (previousSection is not null)
        {
            var preserved = NavItems.FirstOrDefault(n => n.Section == previousSection);
            if (preserved is not null)
            {
                SelectedNavItem = preserved;
                return;
            }
        }

        SelectedNavItem = NavItems.FirstOrDefault(n => !n.IsHeader);
    }

    partial void OnSelectedNavItemChanged(SettingsNavItem? value)
    {
        OnPropertyChanged(nameof(SelectedSection));

        if (value?.Section is LlamaCppSettingsViewModel llamaCpp
            && llamaCpp.RefreshServerInfoCommand.CanExecute(null))
        {
            llamaCpp.RefreshServerInfoCommand.Execute(null);
        }
    }
}
