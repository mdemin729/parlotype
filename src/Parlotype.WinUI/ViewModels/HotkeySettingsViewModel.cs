using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;

namespace Parlotype.WinUI.ViewModels;

/// <summary>
/// Manages hotkey configuration and activation mode for the Hotkey Settings page.
/// </summary>
public partial class HotkeySettingsViewModel : ObservableObject
{
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly ISettingsService _settings;
    private readonly ILogger<HotkeySettingsViewModel> _logger;

    [ObservableProperty]
    private HotkeyBinding _currentBinding = HotkeyBinding.Default;

    [ObservableProperty]
    private string _currentBindingDisplay = HotkeyBinding.Default.DisplayString;

    [ObservableProperty]
    private bool _isRecording;

    [ObservableProperty]
    private string _recordingText = "Press keys...";

    [ObservableProperty]
    private bool _hasConflict;

    [ObservableProperty]
    private string? _conflictDescription;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPushToTalk))]
    [NotifyPropertyChangedFor(nameof(IsToggle))]
    private ActivationMode _selectedMode;

    // ── Computed properties ──────────────────────────────────────────

    /// <summary>
    /// Two-way computed property: true when <see cref="SelectedMode"/> is
    /// <see cref="ActivationMode.PushToTalk"/>. Setting to <c>true</c>
    /// switches the activation mode and persists the change.
    /// </summary>
    public bool IsPushToTalk
    {
        get => SelectedMode == ActivationMode.PushToTalk;
        set
        {
            if (value && SelectedMode != ActivationMode.PushToTalk)
                SetActivationModeCommand.Execute(ActivationMode.PushToTalk);
        }
    }

    /// <summary>
    /// Two-way computed property: true when <see cref="SelectedMode"/> is
    /// <see cref="ActivationMode.Toggle"/>. Setting to <c>true</c>
    /// switches the activation mode and persists the change.
    /// </summary>
    public bool IsToggle
    {
        get => SelectedMode == ActivationMode.Toggle;
        set
        {
            if (value && SelectedMode != ActivationMode.Toggle)
                SetActivationModeCommand.Execute(ActivationMode.Toggle);
        }
    }

    // ── Constructors ─────────────────────────────────────────────────

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

    /// <summary>Parameterless constructor for designer support only.</summary>
    public HotkeySettingsViewModel()
        : this(null, new DesignSettingsService())
    {
    }

    // ── Initialization ───────────────────────────────────────────────

    private async Task InitializeAsync()
    {
        try
        {
            // Load persisted binding.
            var savedModifiers = await _settings.GetAsync<string>(SettingsKeys.HotkeyModifiers);
            var savedKey = await _settings.GetAsync<string>(SettingsKeys.HotkeyKey);

            if (Enum.TryParse<HotkeyModifiers>(savedModifiers, out var modifiers)
                && !string.IsNullOrWhiteSpace(savedKey))
            {
                CurrentBinding = new HotkeyBinding(modifiers, savedKey);
            }
            else if (_hotkeyService is not null)
            {
                CurrentBinding = _hotkeyService.CurrentBinding;
            }

            // Load persisted activation mode.
            var savedMode = await _settings.GetAsync<string>(SettingsKeys.ActivationMode);
            if (Enum.TryParse<ActivationMode>(savedMode, out var mode))
            {
                SelectedMode = mode;
            }
            else if (_hotkeyService is not null)
            {
                SelectedMode = _hotkeyService.Mode;
            }

            _logger.LogDebug(
                "Hotkey settings initialized: {Binding}, mode={Mode}",
                CurrentBinding.DisplayString,
                SelectedMode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load hotkey settings");
        }
    }

    // ── Property-change callbacks ────────────────────────────────────

    partial void OnCurrentBindingChanged(HotkeyBinding value)
    {
        if (!IsRecording)
            CurrentBindingDisplay = value.DisplayString;

        var conflict = HotkeyConflictDetector.GetConflictDescription(value);
        ConflictDescription = conflict;
        HasConflict = conflict is not null;
    }

    partial void OnIsRecordingChanged(bool value)
    {
        CurrentBindingDisplay = value ? RecordingText : CurrentBinding.DisplayString;
    }

    // ── Commands ─────────────────────────────────────────────────────

    /// <summary>Enters recording mode so the next keypress is captured.</summary>
    [RelayCommand]
    private void StartRecording()
    {
        IsRecording = true;
        _logger.LogDebug("Hotkey recording started");
    }

    /// <summary>Cancels recording without changing the current binding.</summary>
    [RelayCommand]
    private void StopRecording()
    {
        IsRecording = false;
        _logger.LogDebug("Hotkey recording cancelled");
    }

    /// <summary>
    /// Validates the captured binding, checks for conflicts, updates the
    /// hotkey service, and persists the new binding to settings.
    /// </summary>
    [RelayCommand]
    private async Task ApplyBindingAsync(HotkeyBinding binding)
    {
        IsRecording = false;

        if (!binding.IsValid)
        {
            _logger.LogWarning(
                "Invalid hotkey binding ignored: Modifiers={Modifiers}, Key={Key}",
                binding.Modifiers,
                binding.Key);
            return;
        }

        CurrentBinding = binding;
        _hotkeyService?.UpdateBinding(binding);

        try
        {
            await _settings.SetAsync(SettingsKeys.HotkeyModifiers, binding.Modifiers.ToString());
            await _settings.SetAsync(SettingsKeys.HotkeyKey, binding.Key);
            _logger.LogInformation("Hotkey binding updated to {Binding}", binding.DisplayString);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hotkey binding");
        }
    }

    /// <summary>
    /// Called by the view when keys are captured during recording.
    /// Delegates to <see cref="ApplyBindingCommand"/>.
    /// </summary>
    public void ApplyRecordedBinding(HotkeyBinding binding)
    {
        ApplyBindingCommand.Execute(binding);
    }

    /// <summary>
    /// Updates the activation mode, persists the choice, and notifies the
    /// hotkey service at runtime.
    /// </summary>
    [RelayCommand]
    private async Task SetActivationModeAsync(ActivationMode mode)
    {
        SelectedMode = mode;

        if (_hotkeyService is not null)
            _hotkeyService.Mode = mode;

        try
        {
            await _settings.SetAsync(SettingsKeys.ActivationMode, mode.ToString());
            _logger.LogInformation("Activation mode changed to {Mode}", mode);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist activation mode");
        }
    }

    // ── Design-time stubs ────────────────────────────────────────────

    private sealed class DesignSettingsService : ISettingsService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(default(T));

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
