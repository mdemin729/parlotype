using Microsoft.Extensions.Logging;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using SharpHook;
using SharpHook.Data;

namespace Parlotype.Platform.Hotkeys;

/// <summary>
/// Global hotkey listener using SharpHook's <see cref="SimpleGlobalHook"/>.
/// Supports Push-to-Talk and Toggle activation modes.
/// </summary>
public sealed class SharpHookHotkeyService : IGlobalHotkeyService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<SharpHookHotkeyService> _logger;

    private SimpleGlobalHook? _hook;
    private KeyCode _targetKeyCode;
    private volatile bool _isActive;
    private volatile bool _isToggleRecording;

    public event EventHandler? HotkeyPressed;
    public event EventHandler? HotkeyReleased;

    public HotkeyBinding CurrentBinding { get; private set; } = HotkeyBinding.Default;
    public ActivationMode Mode { get; set; } = ActivationMode.PushToTalk;

    public SharpHookHotkeyService(ISettingsService settings, ILogger<SharpHookHotkeyService> logger)
    {
        _settings = settings;
        _logger = logger;
        ApplyBinding(CurrentBinding);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hook is not null)
            return;

        await LoadSettingsAsync(cancellationToken);

        _hook = new SimpleGlobalHook(GlobalHookType.Keyboard, runAsyncOnBackgroundThread: true);
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;

        _ = _hook.RunAsync();

        _logger.LogInformation(
            "Global hotkey listener started — binding: {Binding}, mode: {Mode}",
            CurrentBinding.DisplayString, Mode);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        DisposeHook();
        _logger.LogInformation("Global hotkey listener stopped");
        return Task.CompletedTask;
    }

    public void UpdateBinding(HotkeyBinding binding)
    {
        ApplyBinding(binding);
        _isActive = false;
        _isToggleRecording = false;
        _logger.LogInformation("Hotkey binding updated to {Binding}", binding.DisplayString);
        _ = PersistSettingsAsync();
    }

    public void Dispose()
    {
        DisposeHook();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode != _targetKeyCode)
            return;

        var modifiers = KeyCodeMapper.ToHotkeyModifiers(e.RawEvent.Mask);
        if (modifiers != CurrentBinding.Modifiers)
            return;

        e.SuppressEvent = true;

        if (Mode == ActivationMode.PushToTalk)
        {
            if (!_isActive)
            {
                _isActive = true;
                _logger.LogDebug("PTT hotkey pressed");
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
        }
        else // Toggle
        {
            if (!_isToggleRecording)
            {
                _isToggleRecording = true;
                _logger.LogDebug("Toggle hotkey: start");
                HotkeyPressed?.Invoke(this, EventArgs.Empty);
            }
            else
            {
                _isToggleRecording = false;
                _logger.LogDebug("Toggle hotkey: stop");
                HotkeyReleased?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e)
    {
        if (e.Data.KeyCode != _targetKeyCode)
            return;

        var modifiers = KeyCodeMapper.ToHotkeyModifiers(e.RawEvent.Mask);
        if (modifiers != CurrentBinding.Modifiers)
            return;

        e.SuppressEvent = true;

        if (Mode != ActivationMode.PushToTalk)
            return;

        if (_isActive)
        {
            _isActive = false;
            _logger.LogDebug("PTT hotkey released");
            HotkeyReleased?.Invoke(this, EventArgs.Empty);
        }
    }

    private void ApplyBinding(HotkeyBinding binding)
    {
        CurrentBinding = binding;
        _targetKeyCode = KeyCodeMapper.ToKeyCode(binding.Key) ?? KeyCode.VcUndefined;

        if (_targetKeyCode == KeyCode.VcUndefined)
            _logger.LogWarning("Unrecognized hotkey key: {Key}", binding.Key);
    }

    private async Task LoadSettingsAsync(CancellationToken cancellationToken)
    {
        var modStr = await _settings.GetAsync<string>(SettingsKeys.HotkeyModifiers, cancellationToken);
        var key = await _settings.GetAsync<string>(SettingsKeys.HotkeyKey, cancellationToken);
        var modeStr = await _settings.GetAsync<string>(SettingsKeys.ActivationMode, cancellationToken);

        if (Enum.TryParse<HotkeyModifiers>(modStr, out var mods) && !string.IsNullOrWhiteSpace(key))
            ApplyBinding(new HotkeyBinding(mods, key));

        if (Enum.TryParse<ActivationMode>(modeStr, out var mode))
            Mode = mode;

        _logger.LogDebug("Loaded hotkey settings — binding: {Binding}, mode: {Mode}",
            CurrentBinding.DisplayString, Mode);
    }

    private async Task PersistSettingsAsync()
    {
        await _settings.SetAsync(SettingsKeys.HotkeyModifiers, CurrentBinding.Modifiers.ToString());
        await _settings.SetAsync(SettingsKeys.HotkeyKey, CurrentBinding.Key);
        await _settings.SetAsync(SettingsKeys.ActivationMode, Mode.ToString());
    }

    private void DisposeHook()
    {
        if (_hook is null)
            return;

        _hook.KeyPressed -= OnKeyPressed;
        _hook.KeyReleased -= OnKeyReleased;

        try
        {
            _hook.Dispose();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error disposing global hook");
        }

        _hook = null;
        _isActive = false;
        _isToggleRecording = false;
    }
}
