using System.ComponentModel;
using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Audio;
using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Bridges <see cref="IGlobalHotkeyService"/> to the window manager and the
/// transcribe view model: gestures come in as intent, and go out as window and
/// recording operations. Also reports recording state back to the hotkey
/// service, which needs it for toggle gestures and for Escape.
/// </summary>
public sealed class HotkeyCoordinator : IDisposable
{
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly IWindowManager _windowManager;
    private readonly TranscribeViewModel _transcribeViewModel;
    private readonly ILogger<HotkeyCoordinator> _logger;
    private bool _started;

    public HotkeyCoordinator(
        IWindowManager windowManager,
        TranscribeViewModel transcribeViewModel,
        ILogger<HotkeyCoordinator> logger,
        IGlobalHotkeyService? hotkeyService = null)
    {
        _windowManager = windowManager;
        _transcribeViewModel = transcribeViewModel;
        _logger = logger;
        _hotkeyService = hotkeyService;

        if (_hotkeyService is not null)
        {
            _hotkeyService.DictationStartRequested += OnStartRequested;
            _hotkeyService.DictationStopRequested += OnStopRequested;
            _hotkeyService.DictationCancelRequested += OnCancelRequested;
            _hotkeyService.BindingsChanged += OnBindingsChanged;
            _transcribeViewModel.PropertyChanged += OnTranscribePropertyChanged;
        }
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hotkeyService is null || _started)
            return;

        try
        {
            await _hotkeyService.StartAsync(cancellationToken);
            _started = true;
            RefreshHotkeyHint();

            _logger.LogInformation("Global hotkey service started ({Bindings})",
                string.Join(", ", _hotkeyService.Bindings.Select(b => b.DisplayString)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start global hotkey service");
        }
    }

    private void OnStartRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(async () =>
        {
            _windowManager.ShowTranscribe(activate: false);
            await _transcribeViewModel.StartRecordingAsync();
        });

    private void OnStopRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(async () =>
        {
            await _transcribeViewModel.StopRecordingAsync();
        });

    private void OnCancelRequested(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(async () =>
        {
            await _transcribeViewModel.CancelRecordingAsync();
        });

    private void OnBindingsChanged(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(RefreshHotkeyHint);

    /// <summary>
    /// Keeps the hotkey service's view of "are we dictating" honest. Recording
    /// also starts and stops from the widget's button and can fail outright
    /// during a model load, none of which the keyboard hook can see.
    /// </summary>
    private void OnTranscribePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(TranscribeViewModel.IsRecording)
            or nameof(TranscribeViewModel.RecordingState)))
            return;

        // Loading counts as active so Escape can abandon a start that is still
        // waiting on the model.
        var active = _transcribeViewModel.IsRecording
                     || _transcribeViewModel.RecordingState == RecordingState.Loading;

        _hotkeyService?.SetDictationActive(active);
    }

    private void RefreshHotkeyHint()
    {
        if (_hotkeyService is null)
            return;

        _transcribeViewModel.SetHotkeyHint(HotkeyHint.Describe(_hotkeyService.Bindings));
    }

    public void Dispose()
    {
        if (_hotkeyService is not null)
        {
            _hotkeyService.DictationStartRequested -= OnStartRequested;
            _hotkeyService.DictationStopRequested -= OnStopRequested;
            _hotkeyService.DictationCancelRequested -= OnCancelRequested;
            _hotkeyService.BindingsChanged -= OnBindingsChanged;
            _transcribeViewModel.PropertyChanged -= OnTranscribePropertyChanged;

            try { _hotkeyService.StopAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to stop hotkey service"); }
        }
    }
}
