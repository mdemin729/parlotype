namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Recognizes "double-tap a bare modifier" for one binding.
/// </summary>
internal sealed class ModifierTapTracker
{
    private readonly HotkeyGesture _gesture;

    private long? _downAtMs;
    private ModifierSide _downSide;
    private bool _otherKeySinceDown;

    private long? _firstTapAtMs;
    private ModifierSide _firstTapSide;

    public ModifierTapTracker(HotkeyGesture gesture) => _gesture = gesture;

    /// <summary>Returns true when this event completed a double-tap.</summary>
    public bool Process(in HotkeyKeyEvent e)
    {
        if (IsOwnModifier(e))
            return e.IsDown ? OnModifierDown(e) : OnModifierUp(e);

        if (!e.IsDown)
            return false;

        // Anything else pressed — a letter, or a different modifier — means the
        // keystrokes around it are ordinary typing, not a double-tap.
        _otherKeySinceDown = true;
        _firstTapAtMs = null;
        return false;
    }

    public void Reset()
    {
        _downAtMs = null;
        _otherKeySinceDown = false;
        _firstTapAtMs = null;
    }

    private bool OnModifierDown(in HotkeyKeyEvent e)
    {
        if (_downAtMs.HasValue)
            return false; // auto-repeat

        _downAtMs = e.TimestampMs;
        _downSide = e.Side;
        _otherKeySinceDown = false;
        return false;
    }

    private bool OnModifierUp(in HotkeyKeyEvent e)
    {
        if (_downAtMs is not { } downAt)
            return false;

        var wasTap = !_otherKeySinceDown
                     && e.TimestampMs - downAt <= HotkeyGestureTiming.TapMaxMs;

        _downAtMs = null;

        if (!wasTap)
        {
            _firstTapAtMs = null;
            return false;
        }

        // A "Ctrl" binding with no side preference still requires both taps on
        // the same physical key — left-then-right is two different keys.
        if (_firstTapAtMs is { } firstTap &&
            _firstTapSide == _downSide &&
            e.TimestampMs - firstTap <= HotkeyGestureTiming.DoubleTapWindowMs)
        {
            _firstTapAtMs = null;
            return true;
        }

        _firstTapAtMs = e.TimestampMs;
        _firstTapSide = _downSide;
        return false;
    }

    private bool IsOwnModifier(in HotkeyKeyEvent e) =>
        e.Modifier == _gesture.Modifier && _gesture.MatchesSide(e.Side);
}
