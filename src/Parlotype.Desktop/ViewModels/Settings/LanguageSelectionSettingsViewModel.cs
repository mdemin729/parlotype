using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Lets the user choose the transcription source language and, for engines that
/// support arbitrary translation (Gemma 4), a target language to translate into.
/// The pickers are engine-aware: Whisper offers its fixed language set for the
/// source and routes English translation through the existing "Whisper output"
/// toggle; an LLM engine offers the full language list for both source and target.
/// Recently used languages are pinned to the top of each picker.
/// </summary>
public partial class LanguageSelectionSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ILogger<LanguageSelectionSettingsViewModel> _logger;

    private LanguageCapabilities _capabilities = SpeechEngineCapabilities.For(SpeechEngine.Whisper);
    private List<string> _recent = [];
    private bool _initialized;

    public override string Title => "Language";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;

    public ObservableCollection<LanguageDisplayItem> SourceLanguages { get; } = [];
    public ObservableCollection<LanguageDisplayItem> TargetLanguages { get; } = [];

    [ObservableProperty]
    private string _selectedSourceCode = LanguageCatalog.AutoDetectCode;

    [ObservableProperty]
    private string _selectedTargetCode = LanguageCatalog.NoTranslationCode;

    /// <summary>True when the active engine can translate into any language.</summary>
    [ObservableProperty]
    private bool _showTargetPicker;

    /// <summary>
    /// True when the active engine is Whisper, where translation is English-only and
    /// lives under the "Whisper output" section rather than a target-language picker.
    /// </summary>
    [ObservableProperty]
    private bool _showWhisperTranslationHint = true;

    /// <summary>Live search text for the source picker.</summary>
    [ObservableProperty]
    private string _sourceFilter = "";

    /// <summary>Live search text for the target picker.</summary>
    [ObservableProperty]
    private string _targetFilter = "";

    /// <summary>True when the source filter matches no languages.</summary>
    [ObservableProperty]
    private bool _sourceHasNoResults;

    /// <summary>True when the target filter matches no languages.</summary>
    [ObservableProperty]
    private bool _targetHasNoResults;

    partial void OnSourceFilterChanged(string value) => RebuildSourceList();

    partial void OnTargetFilterChanged(string value) => RebuildTargetList();

    public LanguageSelectionSettingsViewModel(
        ISettingsService settings,
        TranscribeViewModel? transcribeViewModel = null,
        ILogger<LanguageSelectionSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _transcribeViewModel = transcribeViewModel;
        _logger = logger ?? NullLogger<LanguageSelectionSettingsViewModel>.Instance;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        if (_initialized)
            return;
        _initialized = true;

        SelectedSourceCode = await _settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage)
                             ?? LanguageCatalog.AutoDetectCode;
        SelectedTargetCode = await _settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage)
                             ?? LanguageCatalog.NoTranslationCode;
        _recent = (await _settings.GetAsync<List<string>>(SettingsKeys.RecentLanguages))?.ToList() ?? [];

        var engineStr = await _settings.GetAsync<string>(SettingsKeys.SpeechEngine);
        var engine = Enum.TryParse<SpeechEngine>(engineStr, ignoreCase: true, out var e) ? e : SpeechEngine.Whisper;
        UpdateForEngine(engine);
    }

    /// <summary>
    /// Recomputes the source/target pickers for the given engine. Called from the
    /// settings window when the active engine changes.
    /// </summary>
    public void UpdateForEngine(SpeechEngine engine)
    {
        _capabilities = SpeechEngineCapabilities.For(engine);
        ShowTargetPicker = _capabilities.SupportsArbitraryTranslation;
        ShowWhisperTranslationHint = engine == SpeechEngine.Whisper;
        RebuildSourceList();
        RebuildTargetList();
    }

    private void RebuildSourceList()
    {
        var items = new List<LanguageDisplayItem>();
        // The Auto-detect sentinel is a mode, not a language, so hide it while searching.
        if (_capabilities.SupportsAutoDetect && string.IsNullOrWhiteSpace(SourceFilter))
            items.Add(MakeItem(LanguageCatalog.AutoDetectCode, "Auto-detect", isRecent: false, SelectSourceCommand));

        items.AddRange(BuildOrdered(_capabilities.EffectiveSourceLanguages, SelectSourceCommand, SourceFilter));
        Replace(SourceLanguages, items, SelectedSourceCode);
        SourceHasNoResults = SourceLanguages.Count == 0;
    }

    private void RebuildTargetList()
    {
        if (!_capabilities.SupportsArbitraryTranslation)
        {
            TargetLanguages.Clear();
            TargetHasNoResults = false;
            return;
        }

        var items = new List<LanguageDisplayItem>();
        // "Default (no translation)" is a mode, not a language, so hide it while searching.
        if (string.IsNullOrWhiteSpace(TargetFilter))
            items.Add(MakeItem(LanguageCatalog.NoTranslationCode, "Default (no translation)", isRecent: false, SelectTargetCommand));

        items.AddRange(BuildOrdered(LanguageCatalog.AllLanguages, SelectTargetCommand, TargetFilter));
        Replace(TargetLanguages, items, SelectedTargetCode);
        TargetHasNoResults = TargetLanguages.Count == 0;
    }

    /// <summary>
    /// Builds language rows with recently-used languages pinned on top (marked as
    /// recent), followed by the remaining languages in catalog order. Each language
    /// appears once. Rows are filtered by <paramref name="filter"/> (matched against
    /// English name, native name, or code; blank matches everything).
    /// </summary>
    private IEnumerable<LanguageDisplayItem> BuildOrdered(
        IReadOnlyList<LanguageInfo> supported, System.Windows.Input.ICommand command, string filter)
    {
        var byCode = supported.ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase);
        var pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in _recent)
        {
            if (byCode.TryGetValue(code, out var info) && Matches(info, filter) && pinned.Add(code))
                yield return MakeItem(info.Code, Label(info), isRecent: true, command);
        }

        foreach (var info in supported)
        {
            if (Matches(info, filter) && !pinned.Contains(info.Code))
                yield return MakeItem(info.Code, Label(info), isRecent: false, command);
        }
    }

    private static bool Matches(LanguageInfo info, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var f = filter.Trim();
        return info.EnglishName.Contains(f, StringComparison.OrdinalIgnoreCase)
            || info.NativeName.Contains(f, StringComparison.OrdinalIgnoreCase)
            || info.Code.Contains(f, StringComparison.OrdinalIgnoreCase);
    }

    private static LanguageDisplayItem MakeItem(
        string code, string label, bool isRecent, System.Windows.Input.ICommand command) =>
        new(code, label, isRecent, command);

    private static string Label(LanguageInfo info) =>
        string.Equals(info.EnglishName, info.NativeName, StringComparison.OrdinalIgnoreCase)
            ? info.EnglishName
            : $"{info.EnglishName} — {info.NativeName}";

    private static void Replace(
        ObservableCollection<LanguageDisplayItem> target, List<LanguageDisplayItem> items, string selectedCode)
    {
        target.Clear();
        foreach (var item in items)
        {
            item.IsSelected = string.Equals(item.Code, selectedCode, StringComparison.OrdinalIgnoreCase);
            target.Add(item);
        }
    }

    [RelayCommand]
    private void SelectSource(string code)
    {
        if (string.Equals(code, SelectedSourceCode, StringComparison.OrdinalIgnoreCase))
            return;

        _logger.LogInformation("Source language selected: {Code}", code);
        SelectedSourceCode = code;
        var recentChanged = PromoteRecent(code);
        _ = PersistAsync(SettingsKeys.SelectedSourceLanguage, code, recentChanged);
    }

    [RelayCommand]
    private void SelectTarget(string code)
    {
        if (string.Equals(code, SelectedTargetCode, StringComparison.OrdinalIgnoreCase))
            return;

        _logger.LogInformation("Target language selected: {Code}", code);
        SelectedTargetCode = code;
        var recentChanged = PromoteRecent(code);
        _ = PersistAsync(SettingsKeys.SelectedTargetLanguage, code, recentChanged);
    }

    /// <summary>
    /// Updates the shared MRU and re-renders both pickers. Runs synchronously on the
    /// caller's (UI) thread so the visual update is immediate — doing this in an async
    /// continuation after a file-IO await resumes off the UI thread, which left the list
    /// repainting one selection behind. The MRU is shared, so a change re-renders both
    /// pickers (a target selection affects the source picker's pinned recents and vice
    /// versa). Returns whether the recents actually changed (so persistence can skip a
    /// redundant write when a sentinel was chosen).
    /// </summary>
    private bool PromoteRecent(string code)
    {
        var updated = RecentLanguages.Add(_recent, code).ToList();
        var changed = !updated.SequenceEqual(_recent, StringComparer.OrdinalIgnoreCase);
        if (changed)
            _recent = updated;

        RebuildSourceList();
        RebuildTargetList(); // no-op clear when the target picker is hidden (Whisper)
        return changed;
    }

    private async Task PersistAsync(string key, string code, bool persistRecent)
    {
        await _settings.SetAsync(key, code);
        if (persistRecent)
            await _settings.SetAsync(SettingsKeys.RecentLanguages, _recent);

        // A language change only takes effect on the next recording, so stop any
        // in-progress capture (mirrors the model/output settings behaviour).
        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording after language change ({Key})", key);
            await _transcribeViewModel.StopRecordingAsync();
        }
    }
}
