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
/// </remarks>
public static class HotkeyText
{
    /// <summary>The gesture as the settings list shows it, e.g. "Hold Right Ctrl".</summary>
    public static string Gesture(HotkeyGesture gesture) => gesture.Kind switch
    {
        HotkeyGestureKind.HoldModifier =>
            Strings.Format_Settings_Hotkeys_Gesture_HoldFormat(Modifier(gesture)),
        HotkeyGestureKind.DoubleTapModifier =>
            Strings.Format_Settings_Hotkeys_Gesture_DoubleTapFormat(Modifier(gesture)),

        // A chord is nothing but key names — "Ctrl+Alt+Space" reads the same in
        // every language, so Core's invariant form is already right.
        _ => gesture.DisplayString,
    };

    /// <summary>The activation mode badge, e.g. "Push to talk".</summary>
    public static string Mode(ActivationMode mode) =>
        mode == ActivationMode.PushToTalk
            ? Strings.Settings_Hotkeys_Mode_PushToTalk
            : Strings.Settings_Hotkeys_Mode_Toggle;

    /// <summary>The gesture with its mode — "Hold Right Ctrl — Push to talk".</summary>
    public static string Line(DictationHotkey hotkey) =>
        Strings.Format_Settings_Hotkeys_LineFormat(Gesture(hotkey.Gesture), Mode(hotkey.Mode));

    private static string Modifier(HotkeyGesture gesture)
    {
        var key = gesture.Modifier switch
        {
            ModifierKey.Ctrl => "Ctrl",
            ModifierKey.Alt => "Alt",
            ModifierKey.Shift => "Shift",
            ModifierKey.Meta => "Win",
            _ => gesture.Modifier.ToString(),
        };

        return gesture.Side switch
        {
            ModifierSide.Left => Strings.Format_Settings_Hotkeys_Modifier_LeftFormat(key),
            ModifierSide.Right => Strings.Format_Settings_Hotkeys_Modifier_RightFormat(key),
            _ => key,
        };
    }
}
