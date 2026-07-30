namespace Parlotype.Core.Hotkeys;

/// <summary>
/// A keyboard event normalized away from any particular hooking library, so
/// gesture matching can be written and tested without one.
/// </summary>
public readonly record struct HotkeyKeyEvent
{
    /// <summary>True for key-down, false for key-up.</summary>
    public bool IsDown { get; init; }

    /// <summary>Monotonic event time in milliseconds.</summary>
    public long TimestampMs { get; init; }

    /// <summary>
    /// Platform-agnostic name of a non-modifier key ("Space", "A", "Escape").
    /// Null when the event is a modifier key or an unmapped key.
    /// </summary>
    public string? KeyName { get; init; }

    /// <summary>Set when the event's own key is a modifier; null otherwise.</summary>
    public ModifierKey? Modifier { get; init; }

    /// <summary>Which physical modifier key this is. Meaningful only when <see cref="Modifier"/> is set.</summary>
    public ModifierSide Side { get; init; }

    /// <summary>
    /// Modifiers currently held, as reported by the OS rather than accumulated
    /// from earlier events — a hook that starts while Ctrl is already down, or
    /// misses a key-up during a focus change, would otherwise desync.
    /// </summary>
    public HotkeyModifiers HeldModifiers { get; init; }

    /// <summary>
    /// True when the right Alt key is physically held. On European layouts
    /// AltGr reports as Ctrl+Alt, so chords must ignore these events or typing
    /// an accented character would start dictation.
    /// </summary>
    public bool RightAltHeld { get; init; }

    /// <summary>True when this event's key is a modifier key.</summary>
    public bool IsModifier => Modifier.HasValue;

    public static HotkeyKeyEvent ModifierDown(
        ModifierKey modifier, ModifierSide side, long timestampMs,
        HotkeyModifiers held = HotkeyModifiers.None, bool rightAltHeld = false) =>
        new()
        {
            IsDown = true, TimestampMs = timestampMs, Modifier = modifier,
            Side = side, HeldModifiers = held, RightAltHeld = rightAltHeld
        };

    public static HotkeyKeyEvent ModifierUp(
        ModifierKey modifier, ModifierSide side, long timestampMs,
        HotkeyModifiers held = HotkeyModifiers.None, bool rightAltHeld = false) =>
        new()
        {
            IsDown = false, TimestampMs = timestampMs, Modifier = modifier,
            Side = side, HeldModifiers = held, RightAltHeld = rightAltHeld
        };

    public static HotkeyKeyEvent KeyDown(
        string keyName, long timestampMs,
        HotkeyModifiers held = HotkeyModifiers.None, bool rightAltHeld = false) =>
        new()
        {
            IsDown = true, TimestampMs = timestampMs, KeyName = keyName,
            HeldModifiers = held, RightAltHeld = rightAltHeld
        };

    public static HotkeyKeyEvent KeyUp(
        string keyName, long timestampMs,
        HotkeyModifiers held = HotkeyModifiers.None, bool rightAltHeld = false) =>
        new()
        {
            IsDown = false, TimestampMs = timestampMs, KeyName = keyName,
            HeldModifiers = held, RightAltHeld = rightAltHeld
        };
}
