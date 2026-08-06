namespace Parlotype.Core.Hotkeys;

/// <summary>What a hold tracker concluded from the event it was just given.</summary>
internal enum HoldOutcome
{
    None,

    /// <summary>The key has been held long enough to count — begin dictation.</summary>
    Started,

    /// <summary>The key was released after a real hold — finish and transcribe.</summary>
    Stopped,

    /// <summary>The hold turned out not to be one — discard whatever was captured.</summary>
    Aborted
}

/// <summary>
/// Recognizes "hold a bare modifier to talk" for one binding.
/// </summary>
internal sealed class ModifierHoldTracker
{
    private readonly HotkeyGesture _gesture;

    /// <summary>
    /// When another gesture watches the same physical key, starting on key-down
    /// would make every deliberate double-tap flash a recording into existence
    /// and immediately discard it. Deferring the start past the tap window
    /// costs a little latency and removes the flicker entirely.
    /// </summary>
    private readonly bool _deferStart;

    private bool _holding;
    private bool _startEmitted;
    private bool _aborted;
    private long _holdStartMs;

    public ModifierHoldTracker(HotkeyGesture gesture, bool deferStart)
    {
        _gesture = gesture;
        _deferStart = deferStart;
    }

    /// <summary>Absolute time at which <see cref="ProcessTimeout"/> should be called, if any.</summary>
    public long? PendingTimeoutMs =>
        _holding && !_startEmitted && !_aborted
            ? _holdStartMs + HotkeyGestureTiming.TapMaxMs
            : null;

    public HoldOutcome Process(in HotkeyKeyEvent e)
    {
        if (IsOwnModifier(e))
            return e.IsDown ? OnModifierDown(e) : OnModifierUp(e);

        // Any other key going down means the user was reaching for a shortcut,
        // not dictating — immediately for a command modifier, and only within
        // the grace window otherwise.
        if (e.IsDown && !e.IsModifier && _holding && !_aborted &&
            (IsCommandModifierHold ||
             e.TimestampMs - _holdStartMs <= HotkeyGestureTiming.HoldAbortGraceMs))
        {
            _aborted = true;
            return _startEmitted ? HoldOutcome.Aborted : HoldOutcome.None;
        }

        return HoldOutcome.None;
    }

    /// <summary>Called once the deferred start window has elapsed.</summary>
    public HoldOutcome ProcessTimeout(long nowMs)
    {
        if (PendingTimeoutMs is not { } due || nowMs < due)
            return HoldOutcome.None;

        _startEmitted = true;
        return HoldOutcome.Started;
    }

    public void Reset()
    {
        _holding = false;
        _startEmitted = false;
        _aborted = false;
    }

    private HoldOutcome OnModifierDown(in HotkeyKeyEvent e)
    {
        if (_holding)
            return HoldOutcome.None; // auto-repeat

        _holding = true;
        _aborted = false;
        _holdStartMs = e.TimestampMs;

        if (_deferStart)
        {
            _startEmitted = false;
            return HoldOutcome.None;
        }

        _startEmitted = true;
        return HoldOutcome.Started;
    }

    private HoldOutcome OnModifierUp(in HotkeyKeyEvent e)
    {
        if (!_holding)
            return HoldOutcome.None;

        var heldMs = e.TimestampMs - _holdStartMs;
        var started = _startEmitted;
        var aborted = _aborted;

        _holding = false;
        _startEmitted = false;
        _aborted = false;

        if (!started || aborted)
            return HoldOutcome.None;

        // Too brief to be speech — this was a tap, and a double-tap binding on
        // the same key may still be waiting on it.
        return heldMs < HotkeyGestureTiming.TapMaxMs
            ? HoldOutcome.Aborted
            : HoldOutcome.Stopped;
    }

    /// <summary>
    /// True when this hold is on a modifier that turns every other keystroke
    /// into a command. Nothing typed while Ctrl or Alt is down reaches the
    /// target app as text, so a keypress at any point during the hold — not
    /// just inside <see cref="HotkeyGestureTiming.HoldAbortGraceMs"/> — means
    /// the user is driving their application, and the dictation is discarded.
    /// Shift composes text and Meta is not a shipped gesture, so both keep the
    /// grace window.
    /// </summary>
    private bool IsCommandModifierHold =>
        _gesture.Modifier is ModifierKey.Ctrl or ModifierKey.Alt;

    private bool IsOwnModifier(in HotkeyKeyEvent e) =>
        e.Modifier == _gesture.Modifier && _gesture.MatchesSide(e.Side);
}
