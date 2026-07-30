namespace Parlotype.Core.Hotkeys;

/// <summary>
/// The hotkey set a fresh installation starts with. Two gestures because
/// dictation has two distinct modes — hold to talk for a sentence, toggle for
/// hands-free — plus an explicit chord for environments where bare-modifier
/// detection is unavailable.
/// </summary>
public static class DictationHotkeyDefaults
{
    /// <summary>
    /// Push-to-talk. Right Ctrl is the same physical key on every platform,
    /// comfortable to hold, and — because left and right Ctrl are distinct key
    /// codes — leaves every normal Ctrl shortcut working.
    /// </summary>
    public static DictationHotkey PushToTalk { get; } =
        DictationHotkey.Hold(ModifierKey.Ctrl, ModifierSide.Right);

    /// <summary>
    /// Hands-free toggle. Double-tapping Ctrl is macOS Dictation's own default
    /// on external keyboards, and collides with nothing on Windows or Linux.
    /// </summary>
    public static DictationHotkey Toggle { get; } =
        DictationHotkey.DoubleTap(ModifierKey.Ctrl, ModifierSide.Either);

    /// <summary>
    /// Explicit chord fallback. Space is the safest member of the Ctrl+Alt
    /// family: on European layouts AltGr *is* Ctrl+Alt, so Ctrl+Alt+letter
    /// combinations fire while the user is typing accented characters.
    /// </summary>
    public static DictationHotkey ChordFallback { get; } =
        DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space"),
            ActivationMode.Toggle);

    /// <summary>The full default set, in settings-list order.</summary>
    public static IReadOnlyList<DictationHotkey> All { get; } =
        [PushToTalk, Toggle, ChordFallback];
}
