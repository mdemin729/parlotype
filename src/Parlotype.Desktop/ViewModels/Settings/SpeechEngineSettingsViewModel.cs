using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class SpeechEngineSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ISpeechRecognizer? _recognizer;
    private readonly ILogger<SpeechEngineSettingsViewModel> _logger;

    public override string Title => "Engine";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;

    public SpeechEngineDisplayItem[] EngineOptions { get; }

    [ObservableProperty]
    private SpeechEngine _selectedEngine = SpeechEngine.Parakeet;

    /// <summary>True when Gemma 4 is the selected engine (for conditional UI).</summary>
    [ObservableProperty]
    private bool _isGemma4Selected;

    /// <summary>True when Parakeet is the selected engine (for conditional UI).
    /// Initialized true to match the Parakeet default — the partial-change hook
    /// doesn't fire for field initializers.</summary>
    [ObservableProperty]
    private bool _isParakeetSelected = true;

    /// <summary>True when the OpenAI-compatible cloud engine is selected (for conditional UI).</summary>
    [ObservableProperty]
    private bool _isOpenAiCompatSelected;

    /// <summary>True when the xAI Grok cloud engine is selected (for conditional UI).</summary>
    [ObservableProperty]
    private bool _isXaiGrokSelected;

    /// <summary>
    /// Opt-in: warm the speech model in the background at app startup so the
    /// first record press is instant. Off by default; takes effect on next
    /// launch (ADR-038).
    /// </summary>
    [ObservableProperty]
    private bool _preloadModelOnStartupEnabled;

    partial void OnSelectedEngineChanged(SpeechEngine value)
    {
        IsGemma4Selected = value == SpeechEngine.Gemma4;
        IsParakeetSelected = value == SpeechEngine.Parakeet;
        IsOpenAiCompatSelected = value == SpeechEngine.OpenAiCompatible;
        IsXaiGrokSelected = value == SpeechEngine.XaiGrok;
    }

    partial void OnPreloadModelOnStartupEnabledChanged(bool value)
    {
        _logger.LogInformation("Preload model on startup: {Enabled}", value);
        _ = _settings.SetAsync(SettingsKeys.PrewarmModelOnStartup, value.ToString());
    }

    public SpeechEngineSettingsViewModel(
        ISettingsService settings,
        TranscribeViewModel? transcribeViewModel = null,
        ISpeechRecognizer? recognizer = null,
        ILogger<SpeechEngineSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _transcribeViewModel = transcribeViewModel;
        _recognizer = recognizer;
        _logger = logger ?? NullLogger<SpeechEngineSettingsViewModel>.Instance;

        EngineOptions =
        [
            new(SpeechEngine.Parakeet, "Parakeet v3 (Recommended)",
                "NVIDIA Parakeet via ONNX. ~670 MB download. 25 European languages, auto-detected. Fastest engine — runs on any CPU, no GPU needed. No translation.",
                SelectEngineCommand),
            new(SpeechEngine.Whisper, "Whisper",
                "Local speech recognition via Whisper.net. Well-tested, ~99 languages, source selection and translation to English.",
                SelectEngineCommand),
            new(SpeechEngine.Gemma4, "Gemma 4 (Experimental)",
                "Google Gemma 4 E4B via llama.cpp. Requires ~10 GB download. English only. Best on clean speech.",
                SelectEngineCommand),
            new(SpeechEngine.OpenAiCompatible, "OpenAI-compatible (Cloud)",
                "Cloud transcription via your own OpenAI, Groq, or compatible API key. Audio is sent to the configured provider — nothing runs locally. Fast even on weak hardware. Opt-in; configure your key under Cloud providers below.",
                SelectEngineCommand),
            new(SpeechEngine.XaiGrok, "xAI Grok (Cloud)",
                "xAI Grok Speech-to-Text via your own xAI API key. Audio is sent to xAI — nothing runs locally. Fast even on weak hardware. Opt-in; configure your key under Cloud providers below.",
                SelectEngineCommand),
        ];

        _ = InitializeAsync();
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public SpeechEngineSettingsViewModel() : this(new DesignSettingsService()) { }

    private async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.SpeechEngine);
        var engine = Enum.TryParse<SpeechEngine>(saved, ignoreCase: true, out var parsed)
            ? parsed
            : SpeechEngine.Parakeet;
        Apply(engine);

        var savedPrewarm = await _settings.GetAsync<string>(SettingsKeys.PrewarmModelOnStartup);
        if (bool.TryParse(savedPrewarm, out var prewarm))
            PreloadModelOnStartupEnabled = prewarm;
    }

    [RelayCommand]
    private void SelectEngine(SpeechEngine type)
    {
        if (type == SelectedEngine)
            return;

        _logger.LogInformation("Speech engine selected: {Type}", type);
        Apply(type);
        _ = ApplyEngineChangeAsync(type);
    }

    private async Task ApplyEngineChangeAsync(SpeechEngine type)
    {
        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording before engine change");
            await _transcribeViewModel.StopRecordingAsync();
        }

        if (_recognizer is { IsReady: true })
        {
            _logger.LogInformation("Unloading current speech recognizer");
            await _recognizer.UnloadAsync();
        }

        await _settings.SetAsync(SettingsKeys.SpeechEngine, type.ToString());
    }

    private void Apply(SpeechEngine type)
    {
        SelectedEngine = type;
        foreach (var item in EngineOptions)
            item.IsSelected = item.Type == type;

        // Keep the Transcribe window's cloud-active badge (ADR-032) correct both
        // at startup and on every switch — see SetActiveEngine's docs for why
        // this is a direct call rather than a constructor dependency.
        _transcribeViewModel?.SetActiveEngine(type);
    }

    private sealed class DesignSettingsService : ISettingsService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
