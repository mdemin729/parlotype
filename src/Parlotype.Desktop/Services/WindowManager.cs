using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Threading;
using Microsoft.Extensions.DependencyInjection;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;

namespace Parlotype.Desktop.Services;

internal sealed class WindowManager : IWindowManager
{
    private readonly IServiceProvider _services;
    private TranscribeWindow? _transcribe;
    private SettingsWindow? _settings;

    public WindowManager(IServiceProvider services)
    {
        _services = services;
    }

    public void ShowTranscribe(bool activate = true) => Dispatcher.UIThread.Post(() =>
    {
        if (_transcribe is null || !_transcribe.IsVisible && _transcribe.PlatformImpl is null)
        {
            _transcribe = new TranscribeWindow
            {
                DataContext = _services.GetRequiredService<TranscribeViewModel>(),
            };
            _transcribe.Closing += (_, e) =>
            {
                // Hide instead of close so the app stays in the tray.
                e.Cancel = true;
                _transcribe?.Hide();
            };
        }

        _transcribe.ShowActivated = activate;
        _transcribe.Show();
        _transcribe.WindowState = WindowState.Normal;
        if (activate)
            _transcribe.Activate();
    });

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
