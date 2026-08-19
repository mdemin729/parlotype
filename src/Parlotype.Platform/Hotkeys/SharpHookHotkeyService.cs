using Microsoft.Extensions.Logging;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using SharpHook;
using SharpHook.Data;

namespace Parlotype.Platform.Hotkeys;

/// <summary>
/// Global hotkey listener using SharpHook's <see cref="SimpleGlobalHook"/>.
/// Translates raw key events into <see cref="HotkeyKeyEvent"/> and lets
/// <see cref="HotkeyGestureMatcher"/> decide what they mean, so this class stays
/// a thin adapter over the hooking library.
/// </summary>
public sealed class SharpHookHotkeyService : IGlobalHotkeyService
{
    private readonly ISettingsService _settings;
    private readonly ILogger<SharpHookHotkeyService> _logger;

    /// <summary>Guards the matcher, which the hook thread and the timer both reach.</summary>
    private readonly Lock _matcherLock = new();

    private readonly HotkeyGestureMatcher _matcher = new(DictationHotkeyDefaults.All);

    /// <summary>Fires when a deferred hold has been held long enough to count as one.</summary>
    private readonly Timer _timeoutTimer;

    private SimpleGlobalHook? _hook;
    private bool _disposed;

    public event EventHandler<DictationStartEventArgs>? DictationStartRequested;
    public event EventHandler? DictationStopRequested;
    public event EventHandler? DictationCancelRequested;
    public event EventHandler? BindingsChanged;

    public IReadOnlyList<DictationHotkey> Bindings
    {
        get { lock (_matcherLock) return _matcher.Bindings; }
    }

    public SharpHookHotkeyService(ISettingsService settings, ILogger<SharpHookHotkeyService> logger)
    {
        _settings = settings;
        _logger = logger;
        _timeoutTimer = new Timer(_ => OnTimeout(), null, Timeout.Infinite, Timeout.Infinite);
    }

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_hook is not null)
            return;

        var bindings = await HotkeySettingsMigrator.LoadOrMigrateAsync(_settings, cancellationToken);
        lock (_matcherLock)
            _matcher.UpdateBindings(bindings);

        _hook = new SimpleGlobalHook(GlobalHookType.Keyboard, runAsyncOnBackgroundThread: true);
        _hook.KeyPressed += OnKeyPressed;
        _hook.KeyReleased += OnKeyReleased;

        _ = _hook.RunAsync();

        _logger.LogInformation(
            "Global hotkey listener started — bindings: {Bindings}",
            string.Join(", ", bindings.Select(b => b.DisplayString)));

        // The set may have just been migrated from the legacy single-chord keys.
        BindingsChanged?.Invoke(this, EventArgs.Empty);
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        DisposeHook();
        _logger.LogInformation("Global hotkey listener stopped");
        return Task.CompletedTask;
    }

    public void UpdateBindings(IReadOnlyList<DictationHotkey> bindings)
    {
        lock (_matcherLock)
        {
            _matcher.UpdateBindings(bindings);
            ScheduleTimeout();
        }

        _logger.LogInformation("Hotkey bindings updated: {Bindings}",
            string.Join(", ", bindings.Select(b => b.DisplayString)));

        BindingsChanged?.Invoke(this, EventArgs.Empty);
        _ = PersistBindingsAsync(bindings);
    }

    public void SetDictationActive(bool active)
    {
        lock (_matcherLock)
            _matcher.SetDictationActive(active);
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        DisposeHook();
        _timeoutTimer.Dispose();
    }

    private void OnKeyPressed(object? sender, KeyboardHookEventArgs e) => HandleKey(e, isDown: true);

    private void OnKeyReleased(object? sender, KeyboardHookEventArgs e) => HandleKey(e, isDown: false);

    private void HandleKey(KeyboardHookEventArgs e, bool isDown)
    {
        var keyEvent = BuildKeyEvent(e, isDown);

        HotkeyMatchResult result;
        lock (_matcherLock)
        {
            result = _matcher.Process(keyEvent);
            ScheduleTimeout();
        }

        // Must be set synchronously on the hook thread for SimpleGlobalHook to
        // honour it (ADR-020). Only chords and a cancelling Escape ever suppress —
        // swallowing a bare Ctrl would break every shortcut on the machine.
        if (result.Suppress)
            e.SuppressEvent = true;

        Raise(result.Action, result.HoldScoped);
    }

    private void OnTimeout()
    {
        HotkeyMatchResult result;
        lock (_matcherLock)
        {
            result = _matcher.ProcessTimeout(Environment.TickCount64);
            ScheduleTimeout();
        }

        Raise(result.Action, result.HoldScoped);
    }

    /// <summary>Must be called with <see cref="_matcherLock"/> held.</summary>
    private void ScheduleTimeout()
    {
        if (_disposed)
            return;

        if (_matcher.NextTimeoutMs is not { } due)
        {
            _timeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);
            return;
        }

        var delay = Math.Max(0, due - Environment.TickCount64);
        _timeoutTimer.Change(delay, Timeout.Infinite);
    }

    private static HotkeyKeyEvent BuildKeyEvent(KeyboardHookEventArgs e, bool isDown)
    {
        var mask = e.RawEvent.Mask;
        var modifier = KeyCodeMapper.ToModifier(e.Data.KeyCode);

        return new HotkeyKeyEvent
        {
            IsDown = isDown,
            TimestampMs = Environment.TickCount64,
            KeyName = modifier is null ? KeyCodeMapper.ToKeyName(e.Data.KeyCode) : null,
            Modifier = modifier?.Key,
            Side = modifier?.Side ?? ModifierSide.Either,
            HeldModifiers = KeyCodeMapper.ToHotkeyModifiers(mask),
            RightAltHeld = KeyCodeMapper.IsRightAltHeld(mask)
        };
    }

    private void Raise(DictationAction action, bool holdScoped = false)
    {
        switch (action)
        {
            case DictationAction.Start:
                _logger.LogDebug("Dictation start requested (holdScoped={HoldScoped})", holdScoped);
                DictationStartRequested?.Invoke(this, new DictationStartEventArgs { HoldScoped = holdScoped });
                break;
            case DictationAction.Stop:
                _logger.LogDebug("Dictation stop requested");
                DictationStopRequested?.Invoke(this, EventArgs.Empty);
                break;
            case DictationAction.Cancel:
                _logger.LogDebug("Dictation cancel requested");
                DictationCancelRequested?.Invoke(this, EventArgs.Empty);
                break;
        }
    }

    private async Task PersistBindingsAsync(IReadOnlyList<DictationHotkey> bindings)
    {
        try
        {
            await _settings.SetAsync(SettingsKeys.HotkeyBindings, HotkeyBindingCodec.EncodeAll(bindings));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to persist hotkey bindings");
        }
    }

    private void DisposeHook()
    {
        _timeoutTimer.Change(Timeout.Infinite, Timeout.Infinite);

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
    }
}
