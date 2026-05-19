using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class HotkeySettingsViewModel : SettingsSectionViewModelBase
{
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly ISettingsService _settings;
    private readonly ILogger<HotkeySettingsViewModel> _logger;

    public override string Title => "Hotkey";
    public override SettingsCategory Category => SettingsCategory.Input;

    [ObservableProperty]
    private HotkeyBinding _currentBinding = HotkeyBinding.Default;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPushToTalk))]
    [NotifyPropertyChangedFor(nameof(IsToggle))]
    private ActivationMode _currentMode;

    [ObservableProperty]
    private string _displayText = HotkeyBinding.Default.DisplayString;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string? _conflictWarning;

    public bool IsPushToTalk
    {
        get => CurrentMode == ActivationMode.PushToTalk;
        set { if (value) SetActivationMode(ActivationMode.PushToTalk); }
    }

    public bool IsToggle
    {
        get => CurrentMode == ActivationMode.Toggle;
        set { if (value) SetActivationMode(ActivationMode.Toggle); }
    }

    public HotkeySettingsViewModel(
        IGlobalHotkeyService? hotkeyService,
        ISettingsService settings,
        ILogger<HotkeySettingsViewModel>? logger = null)
    {
        _hotkeyService = hotkeyService;
        _settings = settings;
        _logger = logger ?? NullLogger<HotkeySettingsViewModel>.Instance;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        try
        {
            var savedModifiers = await _settings.GetAsync<string>(SettingsKeys.HotkeyModifiers);
            var savedKey = await _settings.GetAsync<string>(SettingsKeys.HotkeyKey);

            if (Enum.TryParse<HotkeyModifiers>(savedModifiers, out var modifiers) &&
                !string.IsNullOrWhiteSpace(savedKey))
            {
                CurrentBinding = new HotkeyBinding(modifiers, savedKey);
            }

            var savedMode = await _settings.GetAsync<string>(SettingsKeys.ActivationMode);
            if (Enum.TryParse<ActivationMode>(savedMode, out var mode))
                CurrentMode = mode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load hotkey settings");
        }
    }

    partial void OnCurrentBindingChanged(HotkeyBinding value)
    {
        if (!IsRecording)
            DisplayText = value.DisplayString;
        ConflictWarning = HotkeyConflictDetector.GetConflictDescription(value);
    }

    partial void OnIsRecordingChanged(bool value)
    {
        DisplayText = value ? "Press keys..." : CurrentBinding.DisplayString;
    }

    [RelayCommand]
    private void StartRecording() => IsRecording = true;

    [RelayCommand]
    private void StopRecording() => IsRecording = false;

    public void ApplyRecordedBinding(HotkeyBinding binding)
    {
        IsRecording = false;
        if (!binding.IsValid)
            return;

        CurrentBinding = binding;
        _hotkeyService?.UpdateBinding(binding);
        _ = PersistBindingAsync(binding);
        _logger.LogInformation("Hotkey binding updated to {Binding}", binding.DisplayString);
    }

    public void SetActivationMode(ActivationMode mode)
    {
        CurrentMode = mode;
        if (_hotkeyService is not null)
            _hotkeyService.Mode = mode;
        _ = _settings.SetAsync(SettingsKeys.ActivationMode, mode.ToString());
    }

    private async Task PersistBindingAsync(HotkeyBinding binding)
    {
        try
        {
            await _settings.SetAsync(SettingsKeys.HotkeyModifiers, binding.Modifiers.ToString());
            await _settings.SetAsync(SettingsKeys.HotkeyKey, binding.Key);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hotkey binding");
        }
    }
}
