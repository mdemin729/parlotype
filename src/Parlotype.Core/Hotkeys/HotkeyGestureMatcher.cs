namespace Parlotype.Core.Hotkeys;

/// <summary>What the user's keystrokes asked dictation to do.</summary>
public enum DictationAction
{
    None,

    /// <summary>Begin capturing.</summary>
    Start,

    /// <summary>Finish capturing and transcribe.</summary>
    Stop,

    /// <summary>Stop capturing and throw the audio away.</summary>
    Cancel
}

/// <summary>The resolved action plus whether the key event should be hidden from other applications.</summary>
public readonly record struct HotkeyMatchResult(DictationAction Action, bool Suppress)
{
    public static HotkeyMatchResult None { get; } = new(DictationAction.None, false);
}

/// <summary>
/// Resolves raw key events into dictation actions for a whole set of bindings.
/// All the gesture logic lives here rather than in the platform hook so it can
/// be exercised without a keyboard.
/// </summary>
public sealed class HotkeyGestureMatcher
{
    private const string EscapeKeyName = "Escape";

    private readonly List<(DictationHotkey Binding, ModifierHoldTracker Tracker)> _holds = [];
    private readonly List<(DictationHotkey Binding, ModifierTapTracker Tracker)> _taps = [];
    private readonly List<DictationHotkey> _chords = [];

    private bool _dictationActive;

    /// <summary>Key name of the chord currently held down, if any. Also guards against auto-repeat.</summary>
    private string? _heldChordKey;

    /// <summary>True when <see cref="_heldChordKey"/> belongs to a push-to-talk chord, which stops on release.</summary>
    private bool _heldChordIsPushToTalk;

    public HotkeyGestureMatcher(IReadOnlyList<DictationHotkey> bindings)
        => UpdateBindings(bindings);

    public IReadOnlyList<DictationHotkey> Bindings { get; private set; } = [];

    public void UpdateBindings(IReadOnlyList<DictationHotkey> bindings)
    {
        Bindings = bindings;
        _holds.Clear();
        _taps.Clear();
        _chords.Clear();
        _heldChordKey = null;
        _heldChordIsPushToTalk = false;

        var valid = bindings.Where(b => b.IsValid).ToList();

        var doubleTapGestures = valid
            .Where(b => b.Gesture.Kind == HotkeyGestureKind.DoubleTapModifier)
            .Select(b => b.Gesture)
            .ToList();

        foreach (var binding in valid)
        {
            switch (binding.Gesture.Kind)
            {
                case HotkeyGestureKind.HoldModifier:
                    var contested = doubleTapGestures.Any(g => g.SharesModifierKeyWith(binding.Gesture));
                    _holds.Add((binding, new ModifierHoldTracker(binding.Gesture, deferStart: contested)));
                    break;

                case HotkeyGestureKind.DoubleTapModifier:
                    _taps.Add((binding, new ModifierTapTracker(binding.Gesture)));
                    break;

                case HotkeyGestureKind.Chord:
                    _chords.Add(binding);
                    break;
            }
        }
    }

    /// <summary>
    /// Tells the matcher whether dictation is currently running. Toggle gestures
    /// and Escape both need this, and recording can start or stop by other means
    /// (the widget's button, a failed model load), so the owner pushes it in
    /// rather than the matcher guessing.
    /// </summary>
    public void SetDictationActive(bool active)
    {
        _dictationActive = active;
    }

    /// <summary>
    /// When a deferred hold is pending, the time at which <see cref="ProcessTimeout"/>
    /// should be called. Null when nothing is waiting on a clock.
    /// </summary>
    public long? NextTimeoutMs
    {
        get
        {
            long? next = null;
            foreach (var (_, tracker) in _holds)
            {
                if (tracker.PendingTimeoutMs is not { } due)
                    continue;
                if (next is null || due < next)
                    next = due;
            }
            return next;
        }
    }

    public HotkeyMatchResult Process(in HotkeyKeyEvent e)
    {
        var isEscapeCancel = e.IsDown
                             && _dictationActive
                             && string.Equals(e.KeyName, EscapeKeyName, StringComparison.OrdinalIgnoreCase);

        var chord = MatchChord(e);

        // Trackers see every event regardless of what else matched — that is how
        // they notice the keystrokes that disqualify a gesture.
        var gesture = FeedTrackers(e);

        if (isEscapeCancel)
            return new HotkeyMatchResult(DictationAction.Cancel, Suppress: true);

        if (chord.Action != DictationAction.None || chord.Suppress)
            return chord;

        return gesture == DictationAction.None
            ? HotkeyMatchResult.None
            : new HotkeyMatchResult(gesture, Suppress: false);
    }

    /// <summary>Advances any deferred hold whose start window has now elapsed.</summary>
    public HotkeyMatchResult ProcessTimeout(long nowMs)
    {
        foreach (var (_, tracker) in _holds)
        {
            if (tracker.ProcessTimeout(nowMs) == HoldOutcome.Started)
                return new HotkeyMatchResult(DictationAction.Start, Suppress: false);
        }

        return HotkeyMatchResult.None;
    }

    private DictationAction FeedTrackers(in HotkeyKeyEvent e)
    {
        var action = DictationAction.None;

        foreach (var (_, tracker) in _holds)
        {
            var outcome = tracker.Process(e);
            if (outcome == HoldOutcome.None || action != DictationAction.None)
                continue;

            action = outcome switch
            {
                HoldOutcome.Started => DictationAction.Start,
                HoldOutcome.Stopped => DictationAction.Stop,
                HoldOutcome.Aborted => DictationAction.Cancel,
                _ => DictationAction.None
            };
        }

        foreach (var (_, tracker) in _taps)
        {
            if (tracker.Process(e) && action == DictationAction.None)
                action = _dictationActive ? DictationAction.Stop : DictationAction.Start;
        }

        return action;
    }

    private HotkeyMatchResult MatchChord(in HotkeyKeyEvent e)
    {
        if (e.KeyName is null || e.IsModifier)
            return HotkeyMatchResult.None;

        // Releasing a chord key we swallowed on the way down. The release is
        // swallowed too, so the target app never sees a lone key-up (ADR-020).
        if (!e.IsDown)
        {
            if (_heldChordKey is null ||
                !string.Equals(e.KeyName, _heldChordKey, StringComparison.OrdinalIgnoreCase))
                return HotkeyMatchResult.None;

            var wasPushToTalk = _heldChordIsPushToTalk;
            _heldChordKey = null;
            _heldChordIsPushToTalk = false;

            return new HotkeyMatchResult(
                wasPushToTalk ? DictationAction.Stop : DictationAction.None,
                Suppress: true);
        }

        // Held-down keys repeat; a toggle chord would otherwise flip on every repeat.
        if (_heldChordKey is not null)
        {
            return string.Equals(e.KeyName, _heldChordKey, StringComparison.OrdinalIgnoreCase)
                ? new HotkeyMatchResult(DictationAction.None, Suppress: true)
                : HotkeyMatchResult.None;
        }

        // AltGr is Ctrl+Alt on European layouts, so a Ctrl+Alt chord would fire
        // every time the user typed an accented character.
        if (e.RightAltHeld)
            return HotkeyMatchResult.None;

        foreach (var binding in _chords)
        {
            var chord = binding.Gesture.Chord;
            if (chord is null)
                continue;
            if (e.HeldModifiers != chord.Modifiers)
                continue;
            if (!string.Equals(e.KeyName, chord.Key, StringComparison.OrdinalIgnoreCase))
                continue;

            _heldChordKey = chord.Key;
            _heldChordIsPushToTalk = binding.Mode == ActivationMode.PushToTalk;

            var action = _heldChordIsPushToTalk || !_dictationActive
                ? DictationAction.Start
                : DictationAction.Stop;

            return new HotkeyMatchResult(action, Suppress: true);
        }

        return HotkeyMatchResult.None;
    }
}
