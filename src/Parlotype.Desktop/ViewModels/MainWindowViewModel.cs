using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Parlotype.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isHintVisible;

    [RelayCommand]
    private void ToggleRecording()
    {
        IsRecording = !IsRecording;
        StatusText = IsRecording ? "Recording..." : "Ready";
    }

    [RelayCommand]
    private void ToggleSettings()
    {
        IsSettingsOpen = !IsSettingsOpen;
    }

    [RelayCommand]
    private void ToggleHelp()
    {
        // Stub: show hint popup as a demo
        IsHintVisible = !IsHintVisible;
    }

    [RelayCommand]
    private void DismissHint()
    {
        IsHintVisible = false;
    }
}
