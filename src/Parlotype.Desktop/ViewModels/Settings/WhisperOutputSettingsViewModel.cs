using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Output-shaping options that are currently implemented only for Whisper
/// (punctuation, profanity filter, English translation). The section is
/// hidden when Gemma 4 is the active engine.
/// </summary>
public partial class WhisperOutputSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ILogger<WhisperOutputSettingsViewModel> _logger;

    public override string Title => "Whisper output";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;
    public override SpeechEngine? RestrictToEngine => SpeechEngine.Whisper;

    [ObservableProperty]
    private bool _automaticPunctuationEnabled = true;

    [ObservableProperty]
    private bool _filterProfanityEnabled;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TranslationUnavailableNote))]
    [NotifyPropertyChangedFor(nameof(ShowTranslationPausedNote))]
    [NotifyPropertyChangedFor(nameof(ShowTranslationUnavailableNote))]
    private bool _translateToEnglishEnabled;

    /// <summary>
    /// Whether the currently selected Whisper model supports translation. When
    /// false the translate toggle is disabled, but the user's saved preference
    /// (<see cref="TranslateToEnglishEnabled"/>) is preserved so it is restored
    /// when a translation-capable model is selected again.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(TranslationUnavailableNote))]
    [NotifyPropertyChangedFor(nameof(ShowTranslationPausedNote))]
    [NotifyPropertyChangedFor(nameof(ShowTranslationUnavailableNote))]
    private bool _canTranslate = true;

    /// <summary>
    /// Explanatory text shown under the disabled translate toggle. The wording
    /// depends on the user's saved preference: when translation is enabled it
    /// reads as "paused / will resume" (making the preserved intent explicit and
    /// avoiding the apparent contradiction of a greyed-but-checked toggle);
    /// otherwise it simply states the model can't translate.
    /// </summary>
    public string TranslationUnavailableNote => TranslateToEnglishEnabled
        ? "Translation is paused — the selected model can't translate. It resumes automatically when you pick a multilingual model (Medium or Large v1/v2/v3)."
        : "The selected model doesn't support translation. Choose a multilingual model (Medium or Large v1/v2/v3) to use it.";

    /// <summary>
    /// True when translation is enabled by the user but unsupported by the current model.
    /// Used to show the "paused / resumes" note in accent color, signalling preserved intent.
    /// </summary>
    public bool ShowTranslationPausedNote => !CanTranslate && TranslateToEnglishEnabled;

    /// <summary>
    /// True when translation is disabled by the user and unsupported by the current model.
    /// Used to show the neutral "doesn't support translation" note in muted style.
    /// </summary>
    public bool ShowTranslationUnavailableNote => !CanTranslate && !TranslateToEnglishEnabled;

    public WhisperOutputSettingsViewModel(
        ISettingsService settings,
        TranscribeViewModel? transcribeViewModel = null,
        ILogger<WhisperOutputSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _transcribeViewModel = transcribeViewModel;
        _logger = logger ?? NullLogger<WhisperOutputSettingsViewModel>.Instance;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var savedPunctuation = await _settings.GetAsync<string>(SettingsKeys.AutomaticPunctuation);
        if (bool.TryParse(savedPunctuation, out var punct))
            AutomaticPunctuationEnabled = punct;

        var savedProfanity = await _settings.GetAsync<string>(SettingsKeys.FilterProfanity);
        if (bool.TryParse(savedProfanity, out var prof))
            FilterProfanityEnabled = prof;

        var savedTranslate = await _settings.GetAsync<string>(SettingsKeys.TranslateToEnglish);
        if (bool.TryParse(savedTranslate, out var trans))
            TranslateToEnglishEnabled = trans;

        var savedModel = await _settings.GetAsync<string>(SettingsKeys.SelectedWhisperModel);
        var modelType = Enum.TryParse<WhisperModelType>(savedModel, out var m) ? m : WhisperModelType.Base;
        UpdateTranslationAvailability(modelType);
    }

    /// <summary>
    /// Recomputes <see cref="CanTranslate"/> for the given model. Called when the
    /// user switches models in the Whisper model section. Does not alter the
    /// user's saved translate preference.
    /// </summary>
    public void UpdateTranslationAvailability(WhisperModelType model)
    {
        CanTranslate = WhisperModelInfo.Get(model).SupportsTranslation;
    }

    partial void OnAutomaticPunctuationEnabledChanged(bool value)
    {
        _logger.LogInformation("Automatic punctuation: {Enabled}", value);
        _ = SaveAndStopAsync(SettingsKeys.AutomaticPunctuation, value.ToString());
    }

    partial void OnFilterProfanityEnabledChanged(bool value)
    {
        _logger.LogInformation("Filter profanity: {Enabled}", value);
        _ = SaveAndStopAsync(SettingsKeys.FilterProfanity, value.ToString());
    }

    partial void OnTranslateToEnglishEnabledChanged(bool value)
    {
        _logger.LogInformation("Translate to English: {Enabled}", value);
        _ = SaveAndStopAsync(SettingsKeys.TranslateToEnglish, value.ToString());
    }

    private async Task SaveAndStopAsync(string key, string value)
    {
        await _settings.SetAsync(key, value);

        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording after settings change ({Key})", key);
            await _transcribeViewModel.StopRecordingAsync();
        }
    }
}
