using CommunityToolkit.Mvvm.Input;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Top-level view model bound to the tray icon. Exposes commands for
/// the Open / Settings / Exit menu items, and their labels.
/// </summary>
public partial class AppViewModel : ViewModelBase
{
    private readonly IWindowManager _windowManager;

    public AppViewModel(IWindowManager windowManager)
    {
        _windowManager = windowManager;

        // The tray menu is the one surface a language switch cannot reach on its
        // own: NativeMenu is built once when App.axaml loads and never rebuilt,
        // so its headers bind to these properties and are re-raised here
        // (ADR-064). Everything else in the app either uses {loc:Tr} or lives in
        // a window that is recreated.
        Localizer.Instance.CultureChanged += (_, _) =>
        {
            OnPropertyChanged(nameof(TrayOpenLabel));
            OnPropertyChanged(nameof(TraySettingsLabel));
            OnPropertyChanged(nameof(TrayExitLabel));
        };
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public AppViewModel() : this(new DesignWindowManager()) { }

    public string TrayOpenLabel => Strings.Tray_Open;

    public string TraySettingsLabel => Strings.Tray_Settings;

    public string TrayExitLabel => Strings.Tray_Exit;

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
        public void ShowSettings(SettingsSection? section = null) { }
        public void HideTranscribe() { }
        public void Exit() { }
    }
}
