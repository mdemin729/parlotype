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
    private bool _translateToEnglishEnabled;

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
