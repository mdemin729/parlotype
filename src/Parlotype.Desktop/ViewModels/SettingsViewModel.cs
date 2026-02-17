using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parlotype.Core.Audio;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _voiceTypingLauncherEnabled;

    [ObservableProperty]
    private bool _automaticPunctuationEnabled;

    [ObservableProperty]
    private bool _filterProfanityEnabled = true;

    [ObservableProperty]
    private WaitTimeOption _selectedWaitTime = WaitTimeOption.Medium;

    [ObservableProperty]
    private MicrophoneInfo? _selectedMicrophone;

    [ObservableProperty]
    private bool _isWaitTimePickerOpen;

    [ObservableProperty]
    private bool _isMicrophonePickerOpen;

    public ObservableCollection<MicrophoneInfo> AvailableMicrophones { get; } =
    [
        new("mic-1", "Default - Microphone Array (Realtek)", true),
        new("mic-2", "Headset Microphone (USB Audio)", false),
        new("mic-3", "Webcam Microphone (HD Pro)", false)
    ];

    public SettingsViewModel()
    {
        SelectedMicrophone = AvailableMicrophones[0];
    }

    [RelayCommand]
    private void OpenWaitTimePicker()
    {
        IsWaitTimePickerOpen = true;
    }

    [RelayCommand]
    private void OpenMicrophonePicker()
    {
        IsMicrophonePickerOpen = true;
    }

    [RelayCommand]
    private void SelectWaitTime(WaitTimeOption option)
    {
        SelectedWaitTime = option;
        IsWaitTimePickerOpen = false;
    }

    [RelayCommand]
    private void SelectMicrophone(MicrophoneInfo mic)
    {
        SelectedMicrophone = mic;
        IsMicrophonePickerOpen = false;
    }

    [RelayCommand]
    private void GiveFeedback()
    {
        // Stub
    }
}
