using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;

namespace Parlotype.Desktop.Services;

internal sealed class WindowManager : IWindowManager
{
    private readonly IServiceProvider _services;
    private TranscribeWindow? _transcribe;
    private SettingsWindow? _settings;

    /// <summary>
    /// One per <see cref="_transcribe"/> instance, built alongside it in
    /// <see cref="ShowCoreAsync"/> rather than resolved from DI — it watches
    /// one specific window and has no meaning independent of it (ADR-068).
    /// </summary>
    private TranscribeAutoHideController? _autoHide;

    public WindowManager(IServiceProvider services)
    {
        _services = services;
    }

    public void ShowTranscribe(bool activate = true) =>
        Dispatcher.UIThread.Post(async () => await ShowCoreAsync(activate, byDictation: false));

    public void ShowTranscribeForDictation() =>
        Dispatcher.UIThread.Post(async () => await ShowCoreAsync(activate: false, byDictation: true));

    /// <summary>
    /// Creates the Transcribe window on first use and shows it. Shared by
    /// <see cref="ShowTranscribe"/> (tray / relaunch / onboarding — sticky,
    /// FR-3) and <see cref="ShowTranscribeForDictation"/> (a held-key gesture
    /// — transient, ADR-068): the two differ only in whether the window ends
    /// up owned by the user or by the auto-hide controller, which
    /// <paramref name="byDictation"/> decides.
    /// </summary>
    private async Task ShowCoreAsync(bool activate, bool byDictation)
    {
        if (_transcribe is null || !_transcribe.IsVisible && _transcribe.PlatformImpl is null)
        {
            var viewModel = _services.GetRequiredService<TranscribeViewModel>();
            _transcribe = new TranscribeWindow
            {
                DataContext = viewModel,
            };
            _transcribe.Closing += (_, e) =>
            {
                // Hide instead of close so the app stays in the tray.
                e.Cancel = true;
                _transcribe?.Hide();
            };
            // The window is frameless and user-positioned via its grip strip;
            // reopen it where the user left it (ADR-040).
            await _transcribe.RestorePositionAsync(
                _services.GetRequiredService<IWindowStateService>());

            _autoHide = new TranscribeAutoHideController(
                _transcribe,
                viewModel,
                _services.GetRequiredService<ILogger<TranscribeAutoHideController>>());
        }

        // Restore before deciding ownership: a window mid-fade must be back
        // to full opacity before it can be shown again, belt-and-braces
        // alongside HideWithFadeAsync's own finally (ADR-068).
        _transcribe.RestoreChrome();
        _autoHide?.NotifyShowing(byDictation);

        _transcribe.ShowActivated = activate;
        _transcribe.Show();
        _transcribe.WindowState = WindowState.Normal;
        if (activate)
            _transcribe.Activate();
    }

    public void ShowSettings(SettingsSection? section = null) => Dispatcher.UIThread.Post(() =>
    {
        if (_settings is null || _settings.PlatformImpl is null)
        {
            _settings = new SettingsWindow
            {
                DataContext = _services.GetRequiredService<SettingsWindowViewModel>(),
            };
            _settings.Closing += (_, e) =>
            {
                e.Cancel = true;
                _settings?.Hide();
            };
        }

        if (section is not null && _settings.DataContext is SettingsWindowViewModel vm)
            vm.NavigateTo(section.Value);

        _settings.Show();
        _settings.WindowState = WindowState.Normal;
        _settings.Activate();
    });

    public void HideTranscribe() => Dispatcher.UIThread.Post(() => _transcribe?.Hide());

    public void Exit() => Dispatcher.UIThread.Post(() =>
    {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    });
}
