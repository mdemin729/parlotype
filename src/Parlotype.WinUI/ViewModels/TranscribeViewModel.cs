using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;

namespace Parlotype.WinUI.ViewModels;

/// <summary>
/// Manages the recording lifecycle for the compact transcription window.
/// </summary>
public partial class TranscribeViewModel : ObservableObject, IDisposable
{
    private readonly IAudioPipeline? _pipeline;
    private readonly ITextInjectionService? _textInjectionService;
    private readonly ILogger<TranscribeViewModel> _logger;
    private bool _disposed;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(RecordButtonGlyph))]
    private bool _isRecording;

    /// <summary>Icon glyph: microphone when idle, stop when recording.</summary>
    public string RecordButtonGlyph => IsRecording ? "\uE71A" : "\uE720";

    /// <summary>
    /// Raised when the user requests the settings window to be opened.
    /// The host (App) subscribes to this event and handles opening the settings UI.
    /// </summary>
    public event EventHandler? SettingsRequested;

    public TranscribeViewModel(
        IAudioPipeline? pipeline = null,
        ITextInjectionService? textInjectionService = null,
        ILogger<TranscribeViewModel>? logger = null)
    {
        _pipeline = pipeline;
        _textInjectionService = textInjectionService;
        _logger = logger ?? NullLogger<TranscribeViewModel>.Instance;
    }

    public TranscribeViewModel() : this(null, null, null)
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

    [RelayCommand]
    private void OpenSettings()
    {
        SettingsRequested?.Invoke(this, EventArgs.Empty);
    }

    private async Task StartRecordingAsync()
    {
        if (_pipeline is null)
        {
            _logger.LogWarning("Audio pipeline not available — cannot record");
            return;
        }

        if (IsRecording)
            return;

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

        if (!IsRecording)
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_pipeline is not null)
        {
            _pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
        }

        GC.SuppressFinalize(this);
    }
}
