using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;
using Parlotype.Desktop.V2.Services;

namespace Parlotype.Desktop.V2.ViewModels;

/// <summary>
/// Drives the Transcribe window: a Play (toggle record) button and a
/// Settings button that opens the Settings window via <see cref="IWindowManager"/>.
/// Owns audio-pipeline + text-injection wiring lifted from V1's MainWindowViewModel.
/// </summary>
public partial class TranscribeViewModel : ViewModelBase
{
    private readonly IAudioPipeline? _pipeline;
    private readonly ITextInjectionService? _textInjectionService;
    private readonly IWindowManager _windowManager;
    private readonly ILogger<TranscribeViewModel> _logger;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isRecording;

    public TranscribeViewModel(
        IWindowManager windowManager,
        IAudioPipeline? pipeline = null,
        ITextInjectionService? textInjectionService = null,
        ILogger<TranscribeViewModel>? logger = null)
    {
        _windowManager = windowManager;
        _pipeline = pipeline;
        _textInjectionService = textInjectionService;
        _logger = logger ?? NullLogger<TranscribeViewModel>.Instance;
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public TranscribeViewModel() : this(new DesignWindowManager()) { }

    [RelayCommand]
    private async Task TogglePlayAsync()
    {
        if (IsRecording)
            await StopRecordingAsync();
        else
            await StartRecordingAsync();
    }

    [RelayCommand]
    private void OpenSettings() => _windowManager.ShowSettings();

    public async Task StartRecordingAsync()
    {
        if (_pipeline is null || IsRecording)
            return;

        try
        {
            _pipeline.TranscriptionAvailable += OnTranscriptionAvailable;
            await _pipeline.StartAsync(PipelineMode.Batch);
            IsRecording = true;
            StatusText = "Recording...";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            IsRecording = false;
            StatusText = "Ready";
        }
    }

    public async Task StopRecordingAsync()
    {
        if (_pipeline is null || !IsRecording)
            return;

        try
        {
            await _pipeline.StopAsync();
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

    private sealed class DesignWindowManager : IWindowManager
    {
        public void ShowTranscribe() { }
        public void ShowSettings() { }
        public void HideTranscribe() { }
        public void Exit() { }
    }
}
