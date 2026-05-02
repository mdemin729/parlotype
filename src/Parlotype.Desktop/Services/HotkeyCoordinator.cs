using Avalonia.Threading;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Hotkeys;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Services;

/// <summary>
/// Bridges <see cref="IGlobalHotkeyService"/> to V2's window manager and
/// transcribe view model. On hotkey press, opens the Transcribe window AND
/// starts recording. On release (Push-to-Talk), stops recording.
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
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _hotkeyService.HotkeyReleased += OnHotkeyReleased;
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
            _logger.LogInformation("Global hotkey service started ({Mode}, {Binding})",
                _hotkeyService.Mode, _hotkeyService.CurrentBinding.DisplayString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to start global hotkey service");
        }
    }

    private void OnHotkeyPressed(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(async () =>
        {
            _windowManager.ShowTranscribe();
            await _transcribeViewModel.StartRecordingAsync();
        });

    private void OnHotkeyReleased(object? sender, EventArgs e) =>
        Dispatcher.UIThread.Post(async () =>
        {
            await _transcribeViewModel.StopRecordingAsync();
        });

    public void Dispose()
    {
        if (_hotkeyService is not null)
        {
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _hotkeyService.HotkeyReleased -= OnHotkeyReleased;
            try { _hotkeyService.StopAsync().GetAwaiter().GetResult(); }
            catch (Exception ex) { _logger.LogError(ex, "Failed to stop hotkey service"); }
        }
    }
}
