using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Drives the Transcribe window: a Play (toggle record) button and a
/// Settings button that opens the Settings window via <see cref="IWindowManager"/>.
/// Owns audio-pipeline + text-injection wiring lifted from V1's MainWindowViewModel.
/// </summary>
public partial class TranscribeViewModel : ViewModelBase
{
    private readonly IAudioPipeline? _pipeline;
    private readonly ITextInjectionService? _textInjectionService;
    private readonly IAudioLevelProvider? _audioLevelProvider;
    private readonly IWindowManager _windowManager;
    private readonly ILogger<TranscribeViewModel> _logger;

    /// <summary>RMS threshold above which we consider speech active for visual feedback.</summary>
    private const float SpeechThreshold = 0.005f;

    /// <summary>How long Active state persists after speech drops below threshold.</summary>
    private static readonly TimeSpan SpeechHoldOff = TimeSpan.FromMilliseconds(1200);

    /// <summary>Timestamp of the last above-threshold audio level sample.</summary>
    private DateTime _lastSpeechTime;

    /// <summary>Smoothed RMS level for stable state transitions (EMA).</summary>
    private float _smoothedRms;

    /// <summary>EMA factor for rising level (fast attack).</summary>
    private const float RmsAttack = 0.4f;

    /// <summary>EMA factor for falling level (slow decay).</summary>
    private const float RmsDecay = 0.05f;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private RecordingState _recordingState = RecordingState.Disabled;

    [ObservableProperty]
    private float _audioLevel;

    /// <summary>True when recording is active but no speech detected (for button styling).</summary>
    [ObservableProperty]
    private bool _isIdle;

    /// <summary>True when speech is actively detected (for button styling).</summary>
    [ObservableProperty]
    private bool _isActive;

    partial void OnRecordingStateChanged(RecordingState value)
    {
        IsIdle = value == RecordingState.Idle;
        IsActive = value == RecordingState.Active;
    }

    public TranscribeViewModel(
        IWindowManager windowManager,
        IAudioPipeline? pipeline = null,
        ITextInjectionService? textInjectionService = null,
        IAudioLevelProvider? audioLevelProvider = null,
        ILogger<TranscribeViewModel>? logger = null)
    {
        _windowManager = windowManager;
        _pipeline = pipeline;
        _textInjectionService = textInjectionService;
        _audioLevelProvider = audioLevelProvider;
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
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged += OnAudioLevelChanged;
            StatusText = "Loading model...";
            await _pipeline.StartAsync(PipelineMode.Batch);
            IsRecording = true;
            RecordingState = RecordingState.Idle;
            StatusText = "Recording...";
        }
        catch (RuntimeUnavailableException ex)
        {
            _logger.LogWarning(ex, "Whisper runtime '{Runtime}' unavailable", ex.Requested);
            _pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged -= OnAudioLevelChanged;
            IsRecording = false;
            RecordingState = RecordingState.Disabled;
            StatusText = $"{ex.Requested} runtime not available — change in Settings";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start recording");
            _pipeline.TranscriptionAvailable -= OnTranscriptionAvailable;
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged -= OnAudioLevelChanged;
            IsRecording = false;
            RecordingState = RecordingState.Disabled;
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
            if (_audioLevelProvider is not null)
                _audioLevelProvider.LevelChanged -= OnAudioLevelChanged;
            IsRecording = false;
            RecordingState = RecordingState.Disabled;
            AudioLevel = 0f;
            _smoothedRms = 0f;
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

    private void OnAudioLevelChanged(object? sender, AudioLevelEventArgs e)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            AudioLevel = e.Level;

            // Exponential moving average: fast attack, slow decay
            var factor = e.Level > _smoothedRms ? RmsAttack : RmsDecay;
            _smoothedRms += (e.Level - _smoothedRms) * factor;

            if (_smoothedRms >= SpeechThreshold)
            {
                _lastSpeechTime = DateTime.UtcNow;
                RecordingState = RecordingState.Active;
            }
            else if (RecordingState == RecordingState.Active)
            {
                // Hold Active state for SpeechHoldOff after last speech
                if (DateTime.UtcNow - _lastSpeechTime >= SpeechHoldOff)
                    RecordingState = RecordingState.Idle;
            }
        });
    }

    private sealed class DesignWindowManager : IWindowManager
    {
        public void ShowTranscribe(bool activate = true) { }
        public void ShowSettings() { }
        public void HideTranscribe() { }
        public void Exit() { }
    }
}
