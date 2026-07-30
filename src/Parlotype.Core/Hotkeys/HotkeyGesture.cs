namespace Parlotype.Core.Hotkeys;

/// <summary>The three ways a dictation hotkey can be expressed.</summary>
public enum HotkeyGestureKind
{
    /// <summary>Modifiers plus a primary key, e.g. Ctrl+Alt+Space.</summary>
    Chord,

    /// <summary>A bare modifier held down, e.g. Hold Right Ctrl.</summary>
    HoldModifier,

    /// <summary>A bare modifier tapped twice in quick succession, e.g. Double-tap Ctrl.</summary>
    DoubleTapModifier
}

/// <summary>
/// A keyboard gesture that can trigger dictation. Flat rather than a record
/// hierarchy so it round-trips through System.Text.Json without custom
/// converters; <see cref="Kind"/> selects which payload is meaningful.
/// </summary>
public sealed record HotkeyGesture
{
    public HotkeyGestureKind Kind { get; init; }

    /// <summary>Chord payload. Meaningful only when <see cref="Kind"/> is <see cref="HotkeyGestureKind.Chord"/>.</summary>
    public HotkeyBinding? Chord { get; init; }

    /// <summary>Modifier payload for hold and double-tap gestures.</summary>
    public ModifierKey Modifier { get; init; }

    /// <summary>Which physical modifier key the gesture listens to.</summary>
    public ModifierSide Side { get; init; }

    public static HotkeyGesture ForChord(HotkeyBinding chord) =>
        new() { Kind = HotkeyGestureKind.Chord, Chord = chord };

    public static HotkeyGesture ForHold(ModifierKey modifier, ModifierSide side) =>
        new() { Kind = HotkeyGestureKind.HoldModifier, Modifier = modifier, Side = side };

    public static HotkeyGesture ForDoubleTap(ModifierKey modifier, ModifierSide side) =>
        new() { Kind = HotkeyGestureKind.DoubleTapModifier, Modifier = modifier, Side = side };

    /// <summary>True when the payload matching <see cref="Kind"/> is well-formed.</summary>
    public bool IsValid => Kind switch
    {
        HotkeyGestureKind.Chord => Chord is { IsValid: true },
        HotkeyGestureKind.HoldModifier or HotkeyGestureKind.DoubleTapModifier =>
            Enum.IsDefined(Modifier) && Enum.IsDefined(Side),
        _ => false
    };

    /// <summary>Human-readable label, e.g. "Hold Right Ctrl", "Double-tap Ctrl", "Ctrl+Alt+Space".</summary>
    public string DisplayString => Kind switch
    {
        HotkeyGestureKind.Chord => Chord?.DisplayString ?? string.Empty,
        HotkeyGestureKind.HoldModifier => $"Hold {ModifierDisplayName}",
        HotkeyGestureKind.DoubleTapModifier => $"Double-tap {ModifierDisplayName}",
        _ => string.Empty
    };

    private string ModifierDisplayName
    {
        get
        {
            var name = Modifier switch
            {
                ModifierKey.Ctrl => "Ctrl",
                ModifierKey.Alt => "Alt",
                ModifierKey.Shift => "Shift",
                ModifierKey.Meta => "Win",
                _ => Modifier.ToString()
            };

            return Side switch
            {
                ModifierSide.Left => $"Left {name}",
                ModifierSide.Right => $"Right {name}",
                _ => name
            };
        }
    }

    /// <summary>
    /// True when both gestures watch the same physical modifier key, so one
    /// can fire while the other is still deciding. Only meaningful for
    /// bare-modifier gestures; chords never overlap this way.
    /// </summary>
    public bool SharesModifierKeyWith(HotkeyGesture other)
    {
        if (Kind == HotkeyGestureKind.Chord || other.Kind == HotkeyGestureKind.Chord)
            return false;

        if (Modifier != other.Modifier)
            return false;

        return Side == ModifierSide.Either
            || other.Side == ModifierSide.Either
            || Side == other.Side;
    }

    /// <summary>True when a key event for <paramref name="side"/> satisfies this gesture's side filter.</summary>
    public bool MatchesSide(ModifierSide side) =>
        Side == ModifierSide.Either || Side == side;
}
