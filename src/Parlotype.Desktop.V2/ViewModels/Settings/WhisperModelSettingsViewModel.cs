using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.V2.ViewModels;

namespace Parlotype.Desktop.V2.ViewModels.Settings;

public partial class WhisperModelSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IModelDownloadService? _downloadService;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ISpeechRecognizer? _recognizer;
    private readonly ILogger<WhisperModelSettingsViewModel> _logger;

    public override string Title => "Whisper Model";

    public WhisperModelDisplayItem[] ModelOptions { get; }

    [ObservableProperty]
    private WhisperModelType _selectedModel = WhisperModelType.Base;

    public WhisperModelSettingsViewModel(
        ISettingsService settings,
        IModelDownloadService? downloadService = null,
        TranscribeViewModel? transcribeViewModel = null,
        ISpeechRecognizer? recognizer = null,
        ILogger<WhisperModelSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _downloadService = downloadService;
        _transcribeViewModel = transcribeViewModel;
        _recognizer = recognizer;
        _logger = logger ?? NullLogger<WhisperModelSettingsViewModel>.Instance;

        ModelOptions = WhisperModelInfo.GetAll()
            .Select(m => new WhisperModelDisplayItem(
                m,
                _downloadService?.IsModelCached(m.Type) ?? false,
                SelectModelCommand))
            .ToArray();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.SelectedWhisperModel);
        var modelType = Enum.TryParse<WhisperModelType>(saved, out var m) ? m : WhisperModelType.Base;
        Apply(modelType);
    }

    [RelayCommand]
    private void SelectModel(WhisperModelType type)
    {
        if (type == SelectedModel)
            return;

        _logger.LogInformation("Whisper model selected: {Type}", type);
        Apply(type);
        _ = ApplyModelChangeAsync(type);
    }

    private async Task ApplyModelChangeAsync(WhisperModelType type)
    {
        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording before model change");
            await _transcribeViewModel.StopRecordingAsync();
        }

        if (_recognizer is { IsReady: true })
        {
            _logger.LogInformation("Unloading current Whisper model");
            await _recognizer.UnloadAsync();
        }

        await _settings.SetAsync(SettingsKeys.SelectedWhisperModel, type.ToString());
    }

    private void Apply(WhisperModelType type)
    {
        SelectedModel = type;
        foreach (var item in ModelOptions)
            item.IsSelected = item.Type == type;
    }

    public void RefreshCache()
    {
        if (_downloadService is null)
            return;
        foreach (var item in ModelOptions)
            item.IsCached = _downloadService.IsModelCached(item.Type);
    }
}
