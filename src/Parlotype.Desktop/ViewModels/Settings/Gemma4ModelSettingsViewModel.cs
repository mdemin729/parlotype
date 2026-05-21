using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Services;
using Parlotype.Platform.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class Gemma4ModelSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly Gemma4ModelDownloadService? _downloader;
    private readonly Gemma4ModelDownloadDialogService? _downloadDialog;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ISpeechRecognizer? _recognizer;
    private readonly ILogger<Gemma4ModelSettingsViewModel> _logger;

    public override string Title => "Gemma 4 model";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;
    public override SpeechEngine? RestrictToEngine => SpeechEngine.Gemma4;

    public Gemma4ModelDisplayItem[] ModelOptions { get; }

    [ObservableProperty]
    private string _selectedModelId = Gemma4ModelInfo.Default.ModelId;

    public Gemma4ModelSettingsViewModel(
        ISettingsService settings,
        Gemma4ModelDownloadService? downloader = null,
        Gemma4ModelDownloadDialogService? downloadDialog = null,
        TranscribeViewModel? transcribeViewModel = null,
        ISpeechRecognizer? recognizer = null,
        ILogger<Gemma4ModelSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _downloader = downloader;
        _downloadDialog = downloadDialog;
        _transcribeViewModel = transcribeViewModel;
        _recognizer = recognizer;
        _logger = logger ?? NullLogger<Gemma4ModelSettingsViewModel>.Instance;

        ModelOptions = Gemma4ModelInfo.All
            .Select(m => new Gemma4ModelDisplayItem(
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
        var saved = await _settings.GetAsync<string>(SettingsKeys.SelectedGemma4Model);
        var modelId = Gemma4ModelInfo.GetById(saved)?.ModelId ?? Gemma4ModelInfo.Default.ModelId;
        Apply(modelId);
    }

    [RelayCommand]
    private void SelectModel(string modelId)
    {
        if (modelId == SelectedModelId)
            return;

        _logger.LogInformation("Gemma 4 model selected: {ModelId}", modelId);
        Apply(modelId);
        _ = ApplyModelChangeAsync(modelId);
    }

    private async Task ApplyModelChangeAsync(string modelId)
    {
        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording before Gemma 4 model change");
            await _transcribeViewModel.StopRecordingAsync();
        }

        if (_recognizer is { IsReady: true })
        {
            _logger.LogInformation("Unloading current Gemma 4 model");
            await _recognizer.UnloadAsync();
        }

        await _settings.SetAsync(SettingsKeys.SelectedGemma4Model, modelId);
    }

    [RelayCommand]
    private async Task DownloadModelAsync(string modelId)
    {
        if (_downloadDialog is null)
            return;

        var model = Gemma4ModelInfo.GetById(modelId);
        if (model is null)
            return;

        _logger.LogInformation("Download requested for Gemma 4 model: {ModelId}", modelId);
        await _downloadDialog.EnsureModelAsync(model);
        RefreshInstalledState();
    }

    [RelayCommand]
    private async Task DeleteModelAsync(string modelId)
    {
        if (_downloader is null)
            return;

        var model = Gemma4ModelInfo.GetById(modelId);
        if (model is null)
            return;

        if (_recognizer is { IsReady: true } && modelId == SelectedModelId)
        {
            _logger.LogInformation("Unloading recognizer before deleting active Gemma 4 model");
            await _recognizer.UnloadAsync();
        }

        _logger.LogInformation("Deleting Gemma 4 model: {ModelId}", modelId);
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
            var model = Gemma4ModelInfo.GetById(item.ModelId);
            item.IsInstalled = model is not null && _downloader.IsModelCached(model);
        }
    }
}
