using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;

namespace Parlotype.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly IAudioPipeline? _pipeline;
    private readonly ITextInjectionService? _textInjectionService;
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isHintVisible;

    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(
        SettingsViewModel settings,
        IAudioPipeline? pipeline = null,
        ITextInjectionService? textInjectionService = null,
        ILogger<MainWindowViewModel>? logger = null)
    {
        Settings = settings;
        _pipeline = pipeline;
        _textInjectionService = textInjectionService;
        _logger = logger ?? NullLogger<MainWindowViewModel>.Instance;
    }

    public MainWindowViewModel() : this(new SettingsViewModel())
    {
    }

    [RelayCommand]
    private async Task ToggleRecordingAsync()
    {
        if (!IsRecording)
        {
            await StartRecordingAsync();
        }
        else
        {
            await StopRecordingAsync();
        }
    }

    private async Task StartRecordingAsync()
    {
        if (_pipeline is null)
        {
            _logger.LogWarning("Audio pipeline not available — cannot record");
            return;
        }

        try
        {
            _pipeline.TranscriptionAvailable += OnTranscriptionAvailable;
            await _pipeline.StartAsync(PipelineMode.Batch);
            IsRecording = true;
            StatusText = "Recording...";
            _logger.LogInformation("Recording started");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            IsRecording = false;
            StatusText = "Ready";
        }
    }

    private async Task StopRecordingAsync()
    {
        if (_pipeline is null)
            return;

        try
        {
            await _pipeline.StopAsync();
            _logger.LogInformation("Recording stopped");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to stop recording");
        }
        finally
        {
            _pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            IsRecording = false;
            StatusText = "Ready";
        }
    }

    private async void OnTranscriptionAvailable(object? sender, TranscriptionEventArgs e)
    {
        _logger.LogDebug("Transcription result: {Text} (confidence: {Confidence:F2}, language: {Language})",
            e.Result.Text, e.Result.Confidence, e.Result.DetectedLanguage);

        if (_textInjectionService is null || string.IsNullOrWhiteSpace(e.Result.Text))
            return;

        try
        {
            await _textInjectionService.InjectTextAsync(e.Result.Text);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to inject transcribed text");
        }
    }

    [RelayCommand]
    private void ToggleHelp()
    {
        IsHintVisible = !IsHintVisible;
    }

    [RelayCommand]
    private void DismissHint()
    {
        IsHintVisible = false;
    }
}
