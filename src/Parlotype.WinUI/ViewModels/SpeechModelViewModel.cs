using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.WinUI.ViewModels;

/// <summary>
/// Display wrapper around <see cref="WhisperModelInfo"/> that adds observable selection
/// and cache-status state for data-binding.
/// </summary>
public partial class WhisperModelDisplayInfo : ObservableObject
{
    public WhisperModelInfo Info { get; }

    public WhisperModelType Type => Info.Type;
    public string DisplayName => Info.DisplayName;
    public string SizeText => Info.DiskSize;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isCached;

    public WhisperModelDisplayInfo(WhisperModelInfo info, bool isCached)
    {
        Info = info;
        _isCached = isCached;
    }
}

/// <summary>
/// ViewModel for the Speech Model settings page. Presents available Whisper models,
/// tracks which one is selected, and shows per-model cache status.
/// </summary>
public partial class SpeechModelViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly IModelDownloadService? _downloadService;
    private readonly ILogger<SpeechModelViewModel> _logger;

    [ObservableProperty]
    private WhisperModelType _selectedModel;

    /// <summary>All available Whisper models with display metadata.</summary>
    public WhisperModelDisplayInfo[] ModelOptions { get; }

    public SpeechModelViewModel(
        ISettingsService settings,
        IModelDownloadService? downloadService = null,
        ILogger<SpeechModelViewModel>? logger = null)
    {
        _settings = settings;
        _downloadService = downloadService;
        _logger = logger ?? NullLogger<SpeechModelViewModel>.Instance;

        ModelOptions = WhisperModelInfo.GetAll()
            .Select(info => new WhisperModelDisplayInfo(
                info,
                isCached: _downloadService?.IsModelCached(info.Type) ?? false))
            .ToArray();

        // Default until the persisted value is loaded.
        _selectedModel = WhisperModelType.Base;

        _ = InitializeAsync();
    }

    /// <summary>Design-time / parameterless constructor.</summary>
    public SpeechModelViewModel()
        : this(null!, null, null)
    {
    }

    // ── Initialization ───────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        try
        {
            var saved = await _settings.GetAsync<string>(SettingsKeys.SelectedWhisperModel);

            if (saved is not null && Enum.TryParse<WhisperModelType>(saved, out var parsed))
            {
                SelectedModel = parsed;
            }

            ApplySelection();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load persisted speech-model selection");
        }
    }

    // ── Commands ─────────────────────────────────────────────────────

    [RelayCommand]
    private async Task SelectModelAsync(WhisperModelType model)
    {
        SelectedModel = model;
        ApplySelection();
        RefreshCacheStatus();

        try
        {
            await _settings.SetAsync(SettingsKeys.SelectedWhisperModel, model.ToString());
            _logger.LogInformation("Speech model selection saved: {Model}", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist speech-model selection");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────

    private void ApplySelection()
    {
        foreach (var item in ModelOptions)
        {
            item.IsSelected = item.Type == SelectedModel;
        }
    }

    /// <summary>
    /// Re-checks the local cache for every model and updates <see cref="WhisperModelDisplayInfo.IsCached"/>.
    /// </summary>
    public void RefreshCacheStatus()
    {
        if (_downloadService is null)
            return;

        foreach (var item in ModelOptions)
        {
            item.IsCached = _downloadService.IsModelCached(item.Type);
        }
    }
}
