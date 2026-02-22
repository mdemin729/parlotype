using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Audio;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

public partial class SettingsViewModel : ViewModelBase, IDisposable
{
    private readonly IMicrophoneEnumerator _enumerator;
    private readonly ISettingsService _settings;
    private readonly IModelDownloadService? _downloadService;
    private readonly ILogger<SettingsViewModel> _logger;

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

    [ObservableProperty]
    private AppTheme _selectedTheme;

    [ObservableProperty]
    private WhisperModelType _selectedWhisperModel = WhisperModelType.Base;

    public ObservableCollection<MicrophoneDisplayItem> AvailableMicrophones { get; } = [];

    public WaitTimeDisplayItem[] WaitTimeOptions { get; }

    public ThemeDisplayItem[] ThemeOptions { get; }

    public WhisperModelDisplayItem[] ModelOptions { get; }

    /// <summary>Raised when the user changes the theme.</summary>
    public event EventHandler<AppTheme>? ThemeChanged;

    public SettingsViewModel(IMicrophoneEnumerator enumerator, ISettingsService settings, IModelDownloadService? downloadService = null, ILogger<SettingsViewModel>? logger = null)
    {
        _enumerator = enumerator;
        _settings = settings;
        _downloadService = downloadService;
        _logger = logger ?? NullLogger<SettingsViewModel>.Instance;

        WaitTimeOptions = Enum.GetValues<WaitTimeOption>()
            .Select(o => new WaitTimeDisplayItem(o, SelectWaitTimeCommand))
            .ToArray();

        ThemeOptions =
        [
            new(AppTheme.Default, "Default (system)", SelectThemeCommand),
            new(AppTheme.Light, "Light", SelectThemeCommand),
            new(AppTheme.Dark, "Dark", SelectThemeCommand),
        ];

        ModelOptions = WhisperModelInfo.GetAll()
            .Select(m => new WhisperModelDisplayItem(m, _downloadService?.IsModelCached(m.Type) ?? false, SelectWhisperModelCommand))
            .ToArray();

        _enumerator.DevicesChanged += OnDevicesChanged;
        _ = InitializeAsync();
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public SettingsViewModel() : this(new DesignMicrophoneEnumerator(), new DesignSettingsService())
    {
    }

    private async Task InitializeAsync()
    {
        var savedTheme = await _settings.GetAsync<string>(SettingsKeys.SelectedTheme);
        if (Enum.TryParse<AppTheme>(savedTheme, out var theme))
            SelectedTheme = theme;

        var savedModel = await _settings.GetAsync<string>(SettingsKeys.SelectedWhisperModel);
        var modelType = Enum.TryParse<WhisperModelType>(savedModel, out var model)
            ? model
            : WhisperModelType.Base;
        ApplyModelSelection(modelType);

        var savedId = await _settings.GetAsync<string>(SettingsKeys.SelectedMicrophoneId);
        PopulateMicrophoneList();
        _logger.LogDebug("Initialized with {Count} microphones, saved selection: {SavedId}",
            AvailableMicrophones.Count, savedId);

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
            _logger.LogInformation("Removing {Count} microphone(s)", toRemove.Count);
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
            _logger.LogInformation("Auto-selecting newly added microphone: {MicName}", addedItems[0].Name);
            ApplySelection(addedItems[0]);
            _ = PersistSelectionAsync(addedItems[0].Info.Id);
        }
        else if (toRemove.Count > 0 && previousSelectedId is not null && toRemove.Any(r => r.Info.Id == previousSelectedId))
        {
            // Selected device was removed → fallback
            if (AvailableMicrophones.Count > 0)
            {
                _logger.LogInformation("Selected device removed, falling back to: {MicName}", AvailableMicrophones[0].Name);
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
        _logger.LogInformation("Microphone selected: {MicName}", item.Name);
        ApplySelection(item);
        IsMicrophonePickerOpen = false;
        _ = PersistSelectionAsync(item.Info.Id);
    }

    [RelayCommand]
    private void SelectTheme(AppTheme theme)
    {
        _logger.LogInformation("Theme selected: {Theme}", theme);
        SelectedTheme = theme;
        ThemeChanged?.Invoke(this, theme);
        _ = _settings.SetAsync(SettingsKeys.SelectedTheme, theme.ToString());
    }

    [RelayCommand]
    private void SelectWhisperModel(WhisperModelType model)
    {
        _logger.LogInformation("Whisper model selected: {Model}", model);
        ApplyModelSelection(model);
        RefreshModelCacheStatus();
        _ = _settings.SetAsync(SettingsKeys.SelectedWhisperModel, model.ToString());
    }

    private void ApplyModelSelection(WhisperModelType model)
    {
        SelectedWhisperModel = model;
        foreach (var item in ModelOptions)
            item.IsSelected = item.Type == model;
    }

    private void RefreshModelCacheStatus()
    {
        if (_downloadService is null)
            return;

        foreach (var item in ModelOptions)
            item.IsCached = _downloadService.IsModelCached(item.Type);
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
