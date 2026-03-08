using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Speech;
using Parlotype.Core.TextInjection;

namespace Parlotype.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase, IDisposable
{
    private readonly IAudioPipeline? _pipeline;
    private readonly ITextInjectionService? _textInjectionService;
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly ILogger<MainWindowViewModel> _logger;
    private bool _disposed;

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
        IGlobalHotkeyService? hotkeyService = null,
        ILogger<MainWindowViewModel>? logger = null)
    {
        Settings = settings;
        _pipeline = pipeline;
        _textInjectionService = textInjectionService;
        _hotkeyService = hotkeyService;
        _logger = logger ?? NullLogger<MainWindowViewModel>.Instance;

        if (_hotkeyService is not null)
        {
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.HotkeyReleased += OnHotkeyReleased;
        }
    }

    public MainWindowViewModel() : this(new SettingsViewModel())
    {
    }

    public async Task InitializeHotkeyServiceAsync(CancellationToken cancellationToken = default)
    {
        if (_hotkeyService is null)
        {
            _logger.LogWarning("Global hotkey service not available — hotkeys disabled");
            return;
        }

        try
        {
            await _hotkeyService.StartAsync(cancellationToken);
            _logger.LogInformation("Global hotkey service started ({Mode}, {Binding})",
                _hotkeyService.Mode, _hotkeyService.CurrentBinding.DisplayString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start global hotkey service");
        }
    }

    private async void OnHotkeyPressed(object? sender, EventArgs e)
    {
        _logger.LogDebug("Hotkey pressed");
        await Dispatcher.UIThread.InvokeAsync(StartRecordingAsync);
    }

    private async void OnHotkeyReleased(object? sender, EventArgs e)
    {
        _logger.LogDebug("Hotkey released");
        await Dispatcher.UIThread.InvokeAsync(StopRecordingAsync);
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

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;

        if (_hotkeyService is not null)
        {
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyService.HotkeyReleased -= OnHotkeyReleased;

            try
            {
                _hotkeyService.StopAsync().GetAwaiter().GetResult();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to stop global hotkey service during dispose");
            }
        }

        GC.SuppressFinalize(this);
    }
}
