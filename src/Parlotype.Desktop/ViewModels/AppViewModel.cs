using CommunityToolkit.Mvvm.Input;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Top-level view model bound to the tray icon. Exposes commands for
/// the Open / Settings / Exit menu items.
/// </summary>
public partial class AppViewModel : ViewModelBase
{
    private readonly IWindowManager _windowManager;

    public AppViewModel(IWindowManager windowManager)
    {
        _windowManager = windowManager;
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public AppViewModel() : this(new DesignWindowManager()) { }

    [RelayCommand]
    private void Open() => _windowManager.ShowTranscribe();

    [RelayCommand]
    private void OpenSettings() => _windowManager.ShowSettings();

    [RelayCommand]
    private void Exit() => _windowManager.Exit();

    /// <summary>
    /// Invoked when the tray icon itself is clicked (single click on Windows,
    /// menu only on macOS — wired through TrayIcon.Command).
    /// </summary>
    [RelayCommand]
    private void TrayIconClicked() => _windowManager.ShowTranscribe();

    private sealed class DesignWindowManager : IWindowManager
    {
        public void ShowTranscribe(bool activate = true) { }
        public void ShowSettings() { }
        public void HideTranscribe() { }
        public void Exit() { }
    }
}
