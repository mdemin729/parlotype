using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Services;
using Parlotype.Platform.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class ParakeetModelSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ParakeetModelDownloadService? _downloader;
    private readonly ParakeetModelDownloadDialogService? _downloadDialog;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ISpeechRecognizer? _recognizer;
    private readonly ILogger<ParakeetModelSettingsViewModel> _logger;

    public override string Title => "Parakeet model";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;
    public override SpeechEngine? RestrictToEngine => SpeechEngine.Parakeet;

    public ParakeetModelDisplayItem[] ModelOptions { get; }

    [ObservableProperty]
    private string _selectedModelId = ParakeetModelInfo.Default.ModelId;

    public ParakeetModelSettingsViewModel(
        ISettingsService settings,
        ParakeetModelDownloadService? downloader = null,
        ParakeetModelDownloadDialogService? downloadDialog = null,
        TranscribeViewModel? transcribeViewModel = null,
        ISpeechRecognizer? recognizer = null,
        ILogger<ParakeetModelSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _downloader = downloader;
        _downloadDialog = downloadDialog;
        _transcribeViewModel = transcribeViewModel;
        _recognizer = recognizer;
        _logger = logger ?? NullLogger<ParakeetModelSettingsViewModel>.Instance;

        ModelOptions = ParakeetModelInfo.All
            .Select(m => new ParakeetModelDisplayItem(
                m,
                _downloader?.IsModelCached(m) ?? false,
                SelectModelCommand,
                DownloadModelCommand,
                DeleteModelCommand))
            .ToArray();

        Apply(SelectedModelId);

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.SelectedParakeetModel);
        var modelId = ParakeetModelInfo.GetById(saved)?.ModelId ?? ParakeetModelInfo.Default.ModelId;
        Apply(modelId);
    }

    [RelayCommand]
    private void SelectModel(string modelId)
    {
        if (modelId == SelectedModelId)
            return;

        _logger.LogInformation("Parakeet model selected: {ModelId}", modelId);
        Apply(modelId);
        _ = ApplyModelChangeAsync(modelId);
    }

    private async Task ApplyModelChangeAsync(string modelId)
    {
        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording before Parakeet model change");
            await _transcribeViewModel.StopRecordingAsync();
        }

        if (_recognizer is { IsReady: true })
        {
            _logger.LogInformation("Unloading current Parakeet model");
            await _recognizer.UnloadAsync();
        }

        await _settings.SetAsync(SettingsKeys.SelectedParakeetModel, modelId);
    }

    [RelayCommand]
    private async Task DownloadModelAsync(string modelId)
    {
        if (_downloadDialog is null)
            return;

        var model = ParakeetModelInfo.GetById(modelId);
        if (model is null)
            return;

        _logger.LogInformation("Download requested for Parakeet model: {ModelId}", modelId);
        await _downloadDialog.EnsureModelAsync(model);
        RefreshInstalledState();
    }

    [RelayCommand]
    private async Task DeleteModelAsync(string modelId)
    {
        if (_downloader is null)
            return;

        var model = ParakeetModelInfo.GetById(modelId);
        if (model is null)
            return;

        if (_recognizer is { IsReady: true } && modelId == SelectedModelId)
        {
            _logger.LogInformation("Unloading recognizer before deleting active Parakeet model");
            await _recognizer.UnloadAsync();
        }

        _logger.LogInformation("Deleting Parakeet model: {ModelId}", modelId);
        await _downloader.DeleteModelAsync(model);
        RefreshInstalledState();
    }

    private void Apply(string modelId)
    {
        SelectedModelId = modelId;
        foreach (var item in ModelOptions)
            item.IsSelected = item.ModelId == modelId;
    }

    public void RefreshInstalledState()
    {
        if (_downloader is null)
            return;
        foreach (var item in ModelOptions)
        {
            var model = ParakeetModelInfo.GetById(item.ModelId);
            item.IsInstalled = model is not null && _downloader.IsModelCached(model);
        }
    }
}
