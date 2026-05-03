using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class SpeechSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ILogger<SpeechSettingsViewModel> _logger;

    public override string Title => "Speech";

    public WaitTimeDisplayItem[] WaitTimeOptions { get; }

    [ObservableProperty]
    private WaitTimeOption _selectedWaitTime = WaitTimeOption.Medium;

    [ObservableProperty]
    private bool _automaticPunctuationEnabled = true;

    [ObservableProperty]
    private bool _filterProfanityEnabled;

    public SpeechSettingsViewModel(
        ISettingsService settings,
        ILogger<SpeechSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _logger = logger ?? NullLogger<SpeechSettingsViewModel>.Instance;

        WaitTimeOptions = Enum.GetValues<WaitTimeOption>()
            .Select(o => new WaitTimeDisplayItem(o, SelectWaitTimeCommand))
            .ToArray();

        UpdateWaitTimeSelection(SelectedWaitTime);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var savedWaitTime = await _settings.GetAsync<string>(SettingsKeys.WaitTime);
        if (Enum.TryParse<WaitTimeOption>(savedWaitTime, out var wt))
        {
            SelectedWaitTime = wt;
            UpdateWaitTimeSelection(wt);
        }
        else if (!string.IsNullOrEmpty(savedWaitTime))
        {
            // Migrate legacy values (Instant, VeryShort, Short) removed in favor of 500ms minimum
            _logger.LogInformation("Migrating legacy WaitTime '{Legacy}' to Medium", savedWaitTime);
            SelectedWaitTime = WaitTimeOption.Medium;
            UpdateWaitTimeSelection(WaitTimeOption.Medium);
            await _settings.SetAsync(SettingsKeys.WaitTime, WaitTimeOption.Medium.ToString());
        }

        var savedPunctuation = await _settings.GetAsync<string>(SettingsKeys.AutomaticPunctuation);
        if (bool.TryParse(savedPunctuation, out var punct))
            AutomaticPunctuationEnabled = punct;

        var savedProfanity = await _settings.GetAsync<string>(SettingsKeys.FilterProfanity);
        if (bool.TryParse(savedProfanity, out var prof))
            FilterProfanityEnabled = prof;
    }

    [RelayCommand]
    private void SelectWaitTime(WaitTimeOption option)
    {
        _logger.LogInformation("Wait time selected: {WaitTime}", option);
        SelectedWaitTime = option;
        UpdateWaitTimeSelection(option);
        _ = _settings.SetAsync(SettingsKeys.WaitTime, option.ToString());
    }

    private void UpdateWaitTimeSelection(WaitTimeOption selected)
    {
        foreach (var item in WaitTimeOptions)
            item.IsSelected = item.Option == selected;
    }

    partial void OnAutomaticPunctuationEnabledChanged(bool value)
    {
        _logger.LogInformation("Automatic punctuation: {Enabled}", value);
        _ = _settings.SetAsync(SettingsKeys.AutomaticPunctuation, value.ToString());
    }

    partial void OnFilterProfanityEnabledChanged(bool value)
    {
        _logger.LogInformation("Filter profanity: {Enabled}", value);
        _ = _settings.SetAsync(SettingsKeys.FilterProfanity, value.ToString());
    }
}
