using System.Collections.ObjectModel;
using Avalonia.Threading;
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
        PopulateMicrophoneList();

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
        Dispatcher.UIThread.InvokeAsync(() => UpdateMicrophoneListAsync());
    }

    private async Task UpdateMicrophoneListAsync()
    {
        var currentDevices = _enumerator.GetAvailableMicrophones();
        var currentIds = currentDevices.Select(d => d.Id).ToHashSet();
        var existingIds = AvailableMicrophones.Select(m => m.Info.Id).ToHashSet();
        var previousSelectedId = SelectedMicrophone?.Id;

        // Animated removal of items no longer present
        var toRemove = AvailableMicrophones.Where(m => !currentIds.Contains(m.Info.Id)).ToList();
        foreach (var item in toRemove)
            item.ItemOpacity = 0.0;

        if (toRemove.Count > 0)
        {
            await Task.Delay(150); // match transition duration for fade-out
            foreach (var item in toRemove)
                AvailableMicrophones.Remove(item);
        }

        // Add new items (no animation — appear immediately to avoid empty-slot delay)
        var toAdd = currentDevices.Where(d => !existingIds.Contains(d.Id)).ToList();
        var addedItems = new List<MicrophoneDisplayItem>();
        foreach (var mic in toAdd)
        {
            var displayItem = new MicrophoneDisplayItem(mic, SelectMicrophoneCommand);
            AvailableMicrophones.Add(displayItem);
            addedItems.Add(displayItem);
        }

        // Selection logic
        if (addedItems.Count > 0)
        {
            // Auto-select newly added device
            ApplySelection(addedItems[0]);
            _ = PersistSelectionAsync(addedItems[0].Info.Id);
        }
        else if (toRemove.Count > 0 && previousSelectedId is not null && toRemove.Any(r => r.Info.Id == previousSelectedId))
        {
            // Selected device was removed → fallback
            if (AvailableMicrophones.Count > 0)
            {
                ApplySelection(AvailableMicrophones[0]);
                _ = PersistSelectionAsync(AvailableMicrophones[0].Info.Id);
            }
            else
            {
                SelectedMicrophone = null;
            }
        }
    }

    private void PopulateMicrophoneList()
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
        [
            new("mic-1", "Microphone Array (Realtek)", true),
            new("mic-2", "Headset Microphone (USB Audio)", false),
            new("mic-3", "Webcam Microphone (HD Pro)", false)
        ];
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
