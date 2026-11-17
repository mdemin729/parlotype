using System.Text;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Settings;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Localized presentation for hotkeys (ADR-064).
/// </summary>
/// <remarks>
/// <para>
/// The formatting lives here rather than on <see cref="HotkeyGesture"/> because
/// <c>Parlotype.Core</c> has no resources — and because Core's own
/// <c>DisplayString</c> earns its keep as the <em>invariant</em> form: it is what
/// the log lines in <c>SharpHookHotkeyService</c> and <c>HotkeyCoordinator</c>
/// write, and a log that changes language with the UI is a log you cannot grep.
/// The two are deliberately different things: Core's is for machines, this one is
/// for the window. That is also why the modifier-name mapping is repeated here
/// rather than exposed from Core — it is display, and ADR-064 puts display in
/// Desktop.
/// </para>
/// <para>
/// Key names are <b>not</b> translated. "Ctrl", "Alt", "Shift", "Win" and "Space"
/// are what is printed on the user's keyboard, and Windows leaves them alone in
/// every language. Only the words around them are prose — and the side is a
/// format rather than a prefix because Spanish puts it after the key
/// ("Ctrl derecho") where English puts it before ("Right Ctrl").
/// </para>
/// <para>
/// What is printed on the keyboard depends on the keyboard: a Mac has Option
/// where a PC has Alt, and Command where a PC has the Windows key. The names
/// follow the operating system (ADR-077), which is one more reason they are
/// built here and not taken from Core's invariant form, where the same two keys
/// stay "Alt" and "Win" in every log on every machine.
/// </para>
/// </remarks>
public static class HotkeyText
{
    /// <summary>
    /// Whether keys are named as on an Apple keyboard. Follows the operating
    /// system; settable so a test can read either naming on any machine.
    /// </summary>
    internal static bool UseMacKeyNames { get; set; } = OperatingSystem.IsMacOS();

    /// <summary>The gesture as the settings list shows it, e.g. "Hold Right Ctrl".</summary>
    public static string Gesture(HotkeyGesture gesture) => Gesture(gesture, UseMacKeyNames);

    /// <summary>
    /// <see cref="Gesture(HotkeyGesture)"/> with the keyboard naming chosen by
    /// the caller, so both namings can be read on any machine without touching
    /// process-wide state.
    /// </summary>
    internal static string Gesture(HotkeyGesture gesture, bool macKeyNames) => gesture.Kind switch
    {
        HotkeyGestureKind.HoldModifier =>
            Strings.Format_Settings_Hotkeys_Gesture_HoldFormat(Modifier(gesture, macKeyNames)),
        HotkeyGestureKind.DoubleTapModifier =>
            Strings.Format_Settings_Hotkeys_Gesture_DoubleTapFormat(Modifier(gesture, macKeyNames)),

        // A chord is nothing but key names — "Ctrl+Alt+Space" reads the same in
        // every language, so Core's invariant form is already right wherever the
        // keyboard uses those names.
        _ when macKeyNames && gesture.Chord is { } chord => MacChord(chord),
        _ => gesture.DisplayString,
    };

    /// <summary>A chord with Apple key names, in the order Core's invariant form uses.</summary>
    private static string MacChord(HotkeyBinding chord)
    {
        var text = new StringBuilder();

        foreach (var (flag, key) in ChordModifierOrder)
        {
            if (chord.Modifiers.HasFlag(flag))
                text.Append(KeyName(key, macKeyNames: true)).Append('+');
        }

        return text.Append(chord.Key).ToString();
    }

    private static readonly (HotkeyModifiers Flag, ModifierKey Key)[] ChordModifierOrder =
    [
        (HotkeyModifiers.Ctrl, ModifierKey.Ctrl),
        (HotkeyModifiers.Alt, ModifierKey.Alt),
        (HotkeyModifiers.Shift, ModifierKey.Shift),
        (HotkeyModifiers.Meta, ModifierKey.Meta),
    ];

    /// <summary>The name printed on the key, on an Apple keyboard or a PC one.</summary>
    private static string KeyName(ModifierKey key, bool macKeyNames) => key switch
    {
        ModifierKey.Ctrl => "Ctrl",
        ModifierKey.Alt => macKeyNames ? "Option" : "Alt",
        ModifierKey.Shift => "Shift",
        ModifierKey.Meta => macKeyNames ? "Cmd" : "Win",
        _ => key.ToString(),
    };

    /// <summary>The activation mode badge, e.g. "Push to talk".</summary>
    public static string Mode(ActivationMode mode) =>
        mode == ActivationMode.PushToTalk
            ? Strings.Settings_Hotkeys_Mode_PushToTalk
            : Strings.Settings_Hotkeys_Mode_Toggle;

    /// <summary>The gesture with its mode — "Hold Right Ctrl — Push to talk".</summary>
    public static string Line(DictationHotkey hotkey) =>
        Strings.Format_Settings_Hotkeys_LineFormat(Gesture(hotkey.Gesture), Mode(hotkey.Mode));

    /// <summary>
    /// The one-line reminder on the recording widget's record button — "Hold
    /// Right Ctrl to talk · Esc to cancel". Built from
    /// <see cref="HotkeyHint.SelectPrimary"/> rather than
    /// <see cref="HotkeyHint.Describe"/>, which stays the invariant form
    /// (ADR-064 amendment) — the two consumers (<c>HotkeyCoordinator</c>, the
    /// onboarding recap step) never logged it, only ever showed it.
    /// </summary>
    public static string Hint(IReadOnlyList<DictationHotkey>? bindings)
    {
        if (HotkeyHint.SelectPrimary(bindings) is not { } primary)
            return Strings.Hotkey_Hint_None;

        var gesture = Gesture(primary.Gesture);
        var verb = primary.Mode == ActivationMode.PushToTalk
            ? Strings.Format_Hotkey_Hint_TalkFormat(gesture)
            : Strings.Format_Hotkey_Hint_DictateFormat(gesture);

        return Strings.Format_Hotkey_Hint_WithCancelFormat(verb);
    }

    /// <summary>
    /// The warning a rejected or flagged binding shows in Settings. Built from
    /// <see cref="HotkeyConflict.Reason"/> rather than from the conflict's own
    /// <c>Description</c>, which stays the invariant form the log line writes
    /// (ADR-064 amendment).
    /// </summary>
    /// <returns>Null when there is nothing to say.</returns>
    public static string? Conflict(DictationHotkey candidate, HotkeyConflict conflict) =>
        Conflict(candidate, conflict, UseMacKeyNames);

    /// <summary><see cref="Conflict(DictationHotkey, HotkeyConflict)"/> for a keyboard chosen by the caller.</summary>
    internal static string? Conflict(DictationHotkey candidate, HotkeyConflict conflict, bool macKeyNames)
    {
        var gesture = Gesture(candidate.Gesture, macKeyNames);

        return conflict.Reason switch
        {
            HotkeyConflictReason.InvalidCombination => Strings.Settings_Hotkeys_Conflict_Invalid,

            HotkeyConflictReason.AlreadyBound when conflict.ConflictingMode is { } mode =>
                Strings.Format_Settings_Hotkeys_Conflict_AlreadyBoundFormat(gesture, ConflictMode(mode)),

            HotkeyConflictReason.Reserved when conflict.Reserved is { } reserved =>
                Strings.Format_Settings_Hotkeys_Conflict_ReservedFormat(gesture, Reserved(reserved)),

            HotkeyConflictReason.ParameterHints =>
                Strings.Format_Settings_Hotkeys_Conflict_ParameterHintsFormat(gesture),

            // AltGr is a PC key. On a Mac, Ctrl+Option+letter types nothing, so
            // there is no collision to warn about (ADR-077).
            HotkeyConflictReason.AltGrCollision when macKeyNames => null,

            HotkeyConflictReason.AltGrCollision =>
                Strings.Format_Settings_Hotkeys_Conflict_AltGrFormat(gesture),

            _ => null,
        };
    }

    /// <summary>What the OS does with a shortcut Parlotype refuses to take over.</summary>
    public static string Reserved(ReservedShortcut shortcut) => shortcut switch
    {
        ReservedShortcut.LockWorkstation => Strings.Settings_Hotkeys_Reserved_LockWorkstation,
        ReservedShortcut.FileExplorer => Strings.Settings_Hotkeys_Reserved_FileExplorer,
        ReservedShortcut.RunDialog => Strings.Settings_Hotkeys_Reserved_RunDialog,
        ReservedShortcut.ShowDesktop => Strings.Settings_Hotkeys_Reserved_ShowDesktop,
        ReservedShortcut.WindowsSettings => Strings.Settings_Hotkeys_Reserved_WindowsSettings,
        ReservedShortcut.TaskView => Strings.Settings_Hotkeys_Reserved_TaskView,
        ReservedShortcut.ProjectDisplay => Strings.Settings_Hotkeys_Reserved_ProjectDisplay,
        ReservedShortcut.QuickLinkMenu => Strings.Settings_Hotkeys_Reserved_QuickLinkMenu,
        ReservedShortcut.GameBar => Strings.Settings_Hotkeys_Reserved_GameBar,
        ReservedShortcut.Screenshot => Strings.Settings_Hotkeys_Reserved_Screenshot,
        ReservedShortcut.SecurityScreen => Strings.Settings_Hotkeys_Reserved_SecurityScreen,
        ReservedShortcut.WindowsSpeechRecognition => Strings.Settings_Hotkeys_Reserved_WindowsSpeechRecognition,
        ReservedShortcut.SwitchInputSource => Strings.Settings_Hotkeys_Reserved_SwitchInputSource,
        ReservedShortcut.MacQuit => Strings.Settings_Hotkeys_Reserved_MacQuit,
        ReservedShortcut.MacCloseWindow => Strings.Settings_Hotkeys_Reserved_MacCloseWindow,
        ReservedShortcut.Spotlight => Strings.Settings_Hotkeys_Reserved_Spotlight,
        ReservedShortcut.VoiceTyping => Strings.Settings_Hotkeys_Reserved_VoiceTyping,

        // Unreachable while the enum and the resx agree; the parity test is what
        // keeps them agreeing, so fall back to Core's invariant wording rather
        // than throwing at the user.
        _ => HotkeyConflictDetector.Describe(shortcut),
    };

    /// <summary>
    /// The activation mode as it reads inside a conflict sentence — separate
    /// from <see cref="Mode"/> because English wants it lowercase there and the
    /// badge capitalized, and other languages disagree about which.
    /// </summary>
    private static string ConflictMode(ActivationMode mode) =>
        mode == ActivationMode.PushToTalk
            ? Strings.Settings_Hotkeys_Conflict_Mode_PushToTalk
            : Strings.Settings_Hotkeys_Conflict_Mode_Toggle;

    private static string Modifier(HotkeyGesture gesture, bool macKeyNames)
    {
        var key = KeyName(gesture.Modifier, macKeyNames);

        return gesture.Side switch
        {
            ModifierSide.Left => Strings.Format_Settings_Hotkeys_Modifier_LeftFormat(key),
            ModifierSide.Right => Strings.Format_Settings_Hotkeys_Modifier_RightFormat(key),
            _ => key,
        };
    }
}
