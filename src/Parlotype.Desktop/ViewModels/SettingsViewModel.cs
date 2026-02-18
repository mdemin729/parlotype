using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IMicrophoneEnumerator _enumerator;
    private readonly ISettingsService _settings;

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

    public ObservableCollection<MicrophoneDisplayItem> AvailableMicrophones { get; } = [];

    public WaitTimeDisplayItem[] WaitTimeOptions { get; }

    public SettingsViewModel(IMicrophoneEnumerator enumerator, ISettingsService settings)
    {
        _enumerator = enumerator;
        _settings = settings;

        WaitTimeOptions = Enum.GetValues<WaitTimeOption>()
            .Select(o => new WaitTimeDisplayItem(o, SelectWaitTimeCommand))
            .ToArray();

        _enumerator.DevicesChanged += OnDevicesChanged;
        _ = InitializeMicrophonesAsync();
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public SettingsViewModel() : this(new DesignMicrophoneEnumerator(), new DesignSettingsService())
    {
    }

    private async Task InitializeMicrophonesAsync()
    {
        var savedId = await _settings.GetAsync<string>(SettingsKeys.SelectedMicrophoneId);
        RefreshMicrophoneList();

        // Restore persisted selection or fall back to default/first
        var match = AvailableMicrophones.FirstOrDefault(m => m.Info.Id == savedId);
        if (match is not null)
        {
            ApplySelection(match);
        }
        else if (AvailableMicrophones.Count > 0)
        {
            ApplySelection(AvailableMicrophones[0]);
        }
    }

    private void OnDevicesChanged(object? sender, EventArgs e)
    {
        // Capture the current IDs before refresh
        var previousIds = AvailableMicrophones.Select(m => m.Info.Id).ToHashSet();
        var previousSelectedId = SelectedMicrophone?.Id;

        RefreshMicrophoneList();

        var currentIds = AvailableMicrophones.Select(m => m.Info.Id).ToHashSet();

        // Check for newly added devices
        var addedIds = currentIds.Except(previousIds).ToList();
        if (addedIds.Count > 0)
        {
            var newItem = AvailableMicrophones.First(m => addedIds.Contains(m.Info.Id));
            ApplySelection(newItem);
            _ = PersistSelectionAsync(newItem.Info.Id);
            return;
        }

        // Check if selected device was removed → fallback to first available
        if (previousSelectedId is not null && !currentIds.Contains(previousSelectedId))
        {
            if (AvailableMicrophones.Count > 0)
            {
                ApplySelection(AvailableMicrophones[0]);
                _ = PersistSelectionAsync(AvailableMicrophones[0].Info.Id);
            }
            else
            {
                SelectedMicrophone = null;
            }
            return;
        }

        // Re-apply selection marker if device still present
        var still = AvailableMicrophones.FirstOrDefault(m => m.Info.Id == previousSelectedId);
        if (still is not null)
        {
            ApplySelection(still);
        }
    }

    private void RefreshMicrophoneList()
    {
        AvailableMicrophones.Clear();
        foreach (var mic in _enumerator.GetAvailableMicrophones())
        {
            AvailableMicrophones.Add(new MicrophoneDisplayItem(mic, SelectMicrophoneCommand));
        }
    }

    private void ApplySelection(MicrophoneDisplayItem item)
    {
        foreach (var m in AvailableMicrophones)
            m.IsSelected = false;

        item.IsSelected = true;
        SelectedMicrophone = item.Info;
    }

    private Task PersistSelectionAsync(string micId)
        => _settings.SetAsync(SettingsKeys.SelectedMicrophoneId, micId);

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
    private void SelectMicrophone(MicrophoneDisplayItem item)
    {
        ApplySelection(item);
        IsMicrophonePickerOpen = false;
        _ = PersistSelectionAsync(item.Info.Id);
    }

    [RelayCommand]
    private void GiveFeedback()
    {
        // Stub
    }

    [RelayCommand]
    private void AddNewMicrophone()
    {
        // Stub
    }

    [RelayCommand]
    private void ManageMicrophoneSettings()
    {
        // Stub
    }

    public void Dispose()
    {
        _enumerator.DevicesChanged -= OnDevicesChanged;
        GC.SuppressFinalize(this);
    }

    // Design-time stubs
    private sealed class DesignMicrophoneEnumerator : IMicrophoneEnumerator
    {
        public IReadOnlyList<MicrophoneInfo> GetAvailableMicrophones() =>
            [new("mic-1", "Microphone Array (Realtek)", true)];
        public MicrophoneInfo? GetDefaultMicrophone() => GetAvailableMicrophones()[0];
        public event EventHandler? DevicesChanged { add { } remove { } }
        public void Dispose() { }
    }

    private sealed class DesignSettingsService : ISettingsService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(default(T));
        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
