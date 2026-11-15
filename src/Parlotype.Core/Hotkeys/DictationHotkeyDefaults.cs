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

    /// <summary>
    /// The full default set on Windows and Linux, in settings-list order.
    /// </summary>
    public static IReadOnlyList<DictationHotkey> All { get; } =
        [PushToTalk, Toggle, ChordFallback];

    /// <summary>
    /// Push-to-talk on macOS. MacBook keyboards have no Right Ctrl, so the
    /// shared default would be a key most Mac users cannot press. Right Command
    /// is on every Apple keyboard and almost never used on its own, and left
    /// Command keeps every shortcut (ADR-077).
    /// </summary>
    public static DictationHotkey MacPushToTalk { get; } =
        DictationHotkey.Hold(ModifierKey.Meta, ModifierSide.Right);

    /// <summary>
    /// The default set on macOS. There is no chord: Ctrl+Option+Space is macOS's
    /// own "select next input source", which a suppressed chord would take away
    /// from exactly the multilingual users this app is for, and bare-modifier
    /// detection, the reason the chord exists, works on macOS.
    /// </summary>
    public static IReadOnlyList<DictationHotkey> Mac { get; } =
        [MacPushToTalk, Toggle];

    /// <summary>
    /// The set a fresh installation starts with on the operating system this
    /// process runs on. Runtime code reads this; <see cref="All"/> and
    /// <see cref="Mac"/> stay addressable so each can be tested on any OS.
    /// </summary>
    public static IReadOnlyList<DictationHotkey> ForCurrentPlatform =>
        PinnedSet ?? (OperatingSystem.IsMacOS() ? Mac : All);

    /// <summary>
    /// Test seam. When set, <see cref="ForCurrentPlatform"/> answers this
    /// instead of following the operating system, so a suite written against
    /// one default set asserts the same thing on every machine. Set once,
    /// before any test runs; never used by the app.
    /// </summary>
    internal static IReadOnlyList<DictationHotkey>? PinnedSet { get; set; }
}
