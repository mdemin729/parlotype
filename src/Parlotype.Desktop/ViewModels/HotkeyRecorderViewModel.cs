using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Powers the hotkey recorder component that lets users configure
/// their global hotkey binding and activation mode (Push-to-Talk / Toggle).
/// </summary>
public partial class HotkeyRecorderViewModel : ViewModelBase
{
    private readonly IGlobalHotkeyService? _hotkeyService;
    private readonly ISettingsService _settings;
    private readonly ILogger<HotkeyRecorderViewModel> _logger;

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

    /// <summary>
    /// Two-way computed property: true when <see cref="CurrentMode"/> is
    /// <see cref="ActivationMode.PushToTalk"/>. Setting to <c>true</c>
    /// switches the activation mode and persists the change.
    /// </summary>
    public bool IsPushToTalk
    {
        get => CurrentMode == ActivationMode.PushToTalk;
        set
        {
            if (value) SetActivationMode(ActivationMode.PushToTalk);
        }
    }

    /// <summary>
    /// Two-way computed property: true when <see cref="CurrentMode"/> is
    /// <see cref="ActivationMode.Toggle"/>. Setting to <c>true</c>
    /// switches the activation mode and persists the change.
    /// </summary>
    public bool IsToggle
    {
        get => CurrentMode == ActivationMode.Toggle;
        set
        {
            if (value) SetActivationMode(ActivationMode.Toggle);
        }
    }

    public HotkeyRecorderViewModel(
        IGlobalHotkeyService? hotkeyService,
        ISettingsService settings,
        ILogger<HotkeyRecorderViewModel>? logger = null)
    {
        _hotkeyService = hotkeyService;
        _settings = settings;
        _logger = logger ?? NullLogger<HotkeyRecorderViewModel>.Instance;

        _ = InitializeAsync();
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public HotkeyRecorderViewModel()
        : this(null, new DesignSettingsService())
    {
    }

    private async Task InitializeAsync()
    {
        try
        {
            var savedModifiers = await _settings.GetAsync<string>(SettingsKeys.HotkeyModifiers);
            var savedKey = await _settings.GetAsync<string>(SettingsKeys.HotkeyKey);

            if (Enum.TryParse<HotkeyModifiers>(savedModifiers, out var modifiers)
                && !string.IsNullOrWhiteSpace(savedKey))
            {
                CurrentBinding = new HotkeyBinding(modifiers, savedKey);
            }

            var savedMode = await _settings.GetAsync<string>(SettingsKeys.ActivationMode);
            if (Enum.TryParse<ActivationMode>(savedMode, out var mode))
            {
                CurrentMode = mode;
            }

            _logger.LogDebug(
                "Hotkey recorder initialized: {Binding}, mode={Mode}",
                CurrentBinding.DisplayString,
                CurrentMode);
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
            DisplayText = value.DisplayString;

        ConflictWarning = HotkeyConflictDetector.GetConflictDescription(value);
    }

    partial void OnIsRecordingChanged(bool value)
    {
        DisplayText = value ? "Press keys..." : CurrentBinding.DisplayString;
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

    // ── Public methods called by the view ────────────────────────────

    /// <summary>
    /// Called by the view when keys are captured during recording.
    /// Validates the binding via <see cref="HotkeyConflictDetector"/>,
    /// updates <see cref="CurrentBinding"/>, persists to settings,
    /// and notifies the hotkey service.
    /// </summary>
    public void ApplyRecordedBinding(HotkeyBinding binding)
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
        _ = PersistBindingAsync(binding);

        _logger.LogInformation("Hotkey binding updated to {Binding}", binding.DisplayString);
    }

    /// <summary>
    /// Updates the activation mode, persists the choice, and notifies the
    /// hotkey service at runtime.
    /// </summary>
    public void SetActivationMode(ActivationMode mode)
    {
        CurrentMode = mode;

        if (_hotkeyService is not null)
            _hotkeyService.Mode = mode;

        _ = _settings.SetAsync(SettingsKeys.ActivationMode, mode.ToString());
        _logger.LogInformation("Activation mode changed to {Mode}", mode);
    }

    // ── Persistence helpers ──────────────────────────────────────────

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

    // ── Design-time stubs ────────────────────────────────────────────

    private sealed class DesignSettingsService : ISettingsService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(default(T));

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
            => Task.CompletedTask;
    }
}
