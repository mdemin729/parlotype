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
    public LanguageSelectionSettingsViewModel Language { get; }
    public Gemma4ModelSettingsViewModel Gemma4Model { get; }
    public ParakeetModelSettingsViewModel ParakeetModel { get; }
    public CloudProviderSettingsViewModel CloudProviders { get; }
    public PromptSettingsViewModel Prompts { get; }
    public LlamaCppSettingsViewModel LlamaCpp { get; }
    public HotkeySettingsViewModel Hotkey { get; }
    public ThemeSettingsViewModel Theme { get; }
    public StartupSettingsViewModel Startup { get; }
    public UpdateSettingsViewModel Updates { get; }
    public DataSettingsViewModel Data { get; }
    public HelpSettingsViewModel Help { get; }

    public SettingsWindowViewModel(
        SpeechEngineSettingsViewModel speechEngine,
        MicrophoneSettingsViewModel microphone,
        SilenceTimeoutSettingsViewModel silenceTimeout,
        WhisperModelSettingsViewModel whisperModel,
        RuntimeSettingsViewModel runtime,
        WhisperOutputSettingsViewModel whisperOutput,
        LanguageSelectionSettingsViewModel language,
        Gemma4ModelSettingsViewModel gemma4Model,
        ParakeetModelSettingsViewModel parakeetModel,
        CloudProviderSettingsViewModel cloudProviders,
        PromptSettingsViewModel prompts,
        LlamaCppSettingsViewModel llamaCpp,
        HotkeySettingsViewModel hotkey,
        ThemeSettingsViewModel theme,
        StartupSettingsViewModel startup,
        UpdateSettingsViewModel updates,
        DataSettingsViewModel data,
        HelpSettingsViewModel help)
    {
        SpeechEngine = speechEngine;
        Microphone = microphone;
        SilenceTimeout = silenceTimeout;
        WhisperModel = whisperModel;
        Runtime = runtime;
        WhisperOutput = whisperOutput;
        Language = language;
        Gemma4Model = gemma4Model;
        ParakeetModel = parakeetModel;
        CloudProviders = cloudProviders;
        Prompts = prompts;
        LlamaCpp = llamaCpp;
        Hotkey = hotkey;
        Theme = theme;
        Startup = startup;
        Updates = updates;
        Data = data;
        Help = help;

        // Fixed authoring order — engine-filtered rows are dropped per active
        // engine, but the order of those that survive is preserved.
        _allSections =
        [
            microphone,
            silenceTimeout,
            speechEngine,
            language,
            whisperModel,
            runtime,
            whisperOutput,
            gemma4Model,
            parakeetModel,
            cloudProviders,
            prompts,
            llamaCpp,
            hotkey,
            theme,
            startup,
            updates,
            data,
            help,
        ];

        foreach (var section in _allSections)
            section.NavigationRequested += (_, target) => NavigateTo(target);

        speechEngine.PropertyChanged += OnSpeechEnginePropertyChanged;
        whisperModel.PropertyChanged += OnWhisperModelPropertyChanged;
        language.UpdateForEngine(speechEngine.SelectedEngine);
        language.UpdateTranslationAvailability(whisperModel.SelectedModel);

        RebuildNavItems();
        SelectedNavItem = NavItems.FirstOrDefault(n => !n.IsHeader);
    }

    private void OnSpeechEnginePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpeechEngineSettingsViewModel.SelectedEngine))
        {
            Language.UpdateForEngine(SpeechEngine.SelectedEngine);
            RebuildNavItems();
        }
    }

    private void OnWhisperModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WhisperModelSettingsViewModel.SelectedModel))
            Language.UpdateTranslationAvailability(WhisperModel.SelectedModel);
    }

    private void RebuildNavItems()
    {
        var activeEngine = SpeechEngine.SelectedEngine;
        var previousSection = SelectedNavItem?.Section;

        var visible = _allSections
            .Where(s => s.IsVisibleFor(activeEngine))
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

    /// <summary>
    /// Deep-links the nav selection to a specific section, so external callers
    /// (e.g. the Transcribe strip) can open the window on a chosen page rather
    /// than wherever the user last left it. No-op if the section is not
    /// currently visible for the active engine.
    /// </summary>
    public void NavigateTo(SettingsSection section)
    {
        var target = section switch
        {
            SettingsSection.Language => (SettingsSectionViewModelBase)Language,
            SettingsSection.CloudProviders => CloudProviders,
            SettingsSection.Engine => SpeechEngine,
            SettingsSection.EngineModel => ModelSectionForActiveEngine(),
            SettingsSection.Help => Help,
            _ => null,
        };
        if (target is null)
            return;

        var navItem = NavItems.FirstOrDefault(n => n.Section == target);
        if (navItem is not null)
            SelectedNavItem = navItem;
    }

    /// <summary>
    /// The model page of the active engine (<see cref="SettingsSection.EngineModel"/>).
    /// Cloud engines have no local model page, so they land on the engine list.
    /// </summary>
    private SettingsSectionViewModelBase ModelSectionForActiveEngine() =>
        SpeechEngine.SelectedEngine switch
        {
            Core.Speech.SpeechEngine.Whisper => WhisperModel,
            Core.Speech.SpeechEngine.Gemma4 => Gemma4Model,
            Core.Speech.SpeechEngine.Parakeet => ParakeetModel,
            _ => (SettingsSectionViewModelBase)SpeechEngine,
        };
}
