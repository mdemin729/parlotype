using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Engine-agnostic silence-timeout (post-speech wait) setting. Lives in the
/// Audio category — applies to both Whisper and Gemma 4 pipelines.
/// </summary>
public partial class SilenceTimeoutSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ILogger<SilenceTimeoutSettingsViewModel> _logger;

    public override string Title => "Silence timeout";
    public override SettingsCategory Category => SettingsCategory.Audio;

    public WaitTimeDisplayItem[] WaitTimeOptions { get; }

    [ObservableProperty]
    private WaitTimeOption _selectedWaitTime = WaitTimeOption.Medium;

    public SilenceTimeoutSettingsViewModel(
        ISettingsService settings,
        TranscribeViewModel? transcribeViewModel = null,
        ILogger<SilenceTimeoutSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _transcribeViewModel = transcribeViewModel;
        _logger = logger ?? NullLogger<SilenceTimeoutSettingsViewModel>.Instance;

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
    }

    [RelayCommand]
    private void SelectWaitTime(WaitTimeOption option)
    {
        _logger.LogInformation("Wait time selected: {WaitTime}", option);
        SelectedWaitTime = option;
        UpdateWaitTimeSelection(option);
        _ = SaveAndStopAsync(SettingsKeys.WaitTime, option.ToString());
    }

    private void UpdateWaitTimeSelection(WaitTimeOption selected)
    {
        foreach (var item in WaitTimeOptions)
            item.IsSelected = item.Option == selected;
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
