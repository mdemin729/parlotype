using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Parlotype.Desktop.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    private readonly ILogger<MainWindowViewModel> _logger;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private bool _isSettingsOpen;

    [ObservableProperty]
    private bool _isHintVisible;

    public SettingsViewModel Settings { get; }

    public MainWindowViewModel(SettingsViewModel settings, ILogger<MainWindowViewModel>? logger = null)
    {
        Settings = settings;
        _logger = logger ?? NullLogger<MainWindowViewModel>.Instance;
    }

    public MainWindowViewModel() : this(new SettingsViewModel())
    {
    }

    [RelayCommand]
    private void ToggleRecording()
    {
        IsRecording = !IsRecording;
        StatusText = IsRecording ? "Recording..." : "Ready";
        _logger.LogInformation("Recording toggled: {IsRecording}", IsRecording);
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
