namespace Parlotype.Core.Hotkeys;

/// <summary>
/// One configured dictation hotkey: a gesture plus how it activates recording.
/// Users may configure several of these at once.
/// </summary>
public sealed record DictationHotkey(HotkeyGesture Gesture, ActivationMode Mode)
{
    /// <summary>
    /// Not every gesture works in every mode. Holding a key is inherently
    /// push-to-talk — releasing it has to mean "stop" — and a double-tap has no
    /// release to hang "stop" on, so it has to toggle. Only chords support both.
    /// </summary>
    public static bool IsModeAllowed(HotkeyGestureKind kind, ActivationMode mode) => kind switch
    {
        HotkeyGestureKind.HoldModifier => mode == ActivationMode.PushToTalk,
        HotkeyGestureKind.DoubleTapModifier => mode == ActivationMode.Toggle,
        HotkeyGestureKind.Chord => true,
        _ => false
    };

    /// <summary>The only mode a gesture kind supports, or the sensible default for chords.</summary>
    public static ActivationMode DefaultModeFor(HotkeyGestureKind kind) => kind switch
    {
        HotkeyGestureKind.HoldModifier => ActivationMode.PushToTalk,
        _ => ActivationMode.Toggle
    };

    public bool IsValid => Gesture.IsValid && IsModeAllowed(Gesture.Kind, Mode);

    /// <summary>e.g. "Hold Right Ctrl" / "Ctrl+Alt+Space".</summary>
    public string DisplayString => Gesture.DisplayString;

    /// <summary>Short mode label for the settings list, e.g. "Push to talk".</summary>
    public string ModeLabel => Mode == ActivationMode.PushToTalk ? "Push to talk" : "Toggle";

    public static DictationHotkey Hold(ModifierKey modifier, ModifierSide side) =>
        new(HotkeyGesture.ForHold(modifier, side), ActivationMode.PushToTalk);

    public static DictationHotkey DoubleTap(ModifierKey modifier, ModifierSide side) =>
        new(HotkeyGesture.ForDoubleTap(modifier, side), ActivationMode.Toggle);

    public static DictationHotkey Chord(HotkeyBinding chord, ActivationMode mode) =>
        new(HotkeyGesture.ForChord(chord), mode);
}
