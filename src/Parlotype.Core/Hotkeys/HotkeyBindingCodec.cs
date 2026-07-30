namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Converts <see cref="DictationHotkey"/> to and from a compact text form so a
/// binding set can live in settings.json as a plain string list — readable when
/// someone opens the file, and tolerant of entries written by a newer version
/// (anything that fails to parse is dropped rather than failing the load).
///
/// <para>Format: <c>kind|payload|payload|mode</c>, for example
/// <c>hold|Ctrl|Right|PushToTalk</c>, <c>doubletap|Ctrl|Either|Toggle</c>,
/// <c>chord|Ctrl,Alt|Space|Toggle</c>.</para>
/// </summary>
public static class HotkeyBindingCodec
{
    private const char Separator = '|';
    private const string HoldTag = "hold";
    private const string DoubleTapTag = "doubletap";
    private const string ChordTag = "chord";

    public static string Encode(DictationHotkey hotkey)
    {
        var gesture = hotkey.Gesture;

        return gesture.Kind switch
        {
            HotkeyGestureKind.HoldModifier =>
                string.Join(Separator, HoldTag, gesture.Modifier, gesture.Side, hotkey.Mode),
            HotkeyGestureKind.DoubleTapModifier =>
                string.Join(Separator, DoubleTapTag, gesture.Modifier, gesture.Side, hotkey.Mode),
            HotkeyGestureKind.Chord =>
                string.Join(Separator, ChordTag, EncodeModifiers(gesture.Chord!.Modifiers),
                    gesture.Chord.Key, hotkey.Mode),
            _ => string.Empty
        };
    }

    public static DictationHotkey? Decode(string? encoded)
    {
        if (string.IsNullOrWhiteSpace(encoded))
            return null;

        var parts = encoded.Split(Separator);
        if (parts.Length != 4)
            return null;

        if (!Enum.TryParse<ActivationMode>(parts[3], ignoreCase: true, out var mode))
            return null;

        var gesture = parts[0].ToLowerInvariant() switch
        {
            HoldTag => DecodeModifierGesture(parts[1], parts[2], HotkeyGestureKind.HoldModifier),
            DoubleTapTag => DecodeModifierGesture(parts[1], parts[2], HotkeyGestureKind.DoubleTapModifier),
            ChordTag => DecodeChordGesture(parts[1], parts[2]),
            _ => null
        };

        if (gesture is null)
            return null;

        var hotkey = new DictationHotkey(gesture, mode);
        return hotkey.IsValid ? hotkey : null;
    }

    public static List<string> EncodeAll(IEnumerable<DictationHotkey> hotkeys) =>
        hotkeys.Where(h => h.IsValid).Select(Encode).Where(s => s.Length > 0).ToList();

    public static List<DictationHotkey> DecodeAll(IEnumerable<string>? encoded) =>
        encoded is null
            ? []
            : encoded.Select(Decode).OfType<DictationHotkey>().ToList();

    private static HotkeyGesture? DecodeModifierGesture(string modifier, string side, HotkeyGestureKind kind)
    {
        if (!Enum.TryParse<ModifierKey>(modifier, ignoreCase: true, out var key))
            return null;
        if (!Enum.TryParse<ModifierSide>(side, ignoreCase: true, out var parsedSide))
            return null;

        return kind == HotkeyGestureKind.HoldModifier
            ? HotkeyGesture.ForHold(key, parsedSide)
            : HotkeyGesture.ForDoubleTap(key, parsedSide);
    }

    private static HotkeyGesture? DecodeChordGesture(string modifiers, string key)
    {
        if (!Enum.TryParse<HotkeyModifiers>(modifiers, ignoreCase: true, out var parsedModifiers))
            return null;
        if (string.IsNullOrWhiteSpace(key))
            return null;

        return HotkeyGesture.ForChord(new HotkeyBinding(parsedModifiers, key));
    }

    /// <summary>Flags enums stringify with ", " separators; drop the spaces to keep the form tight.</summary>
    private static string EncodeModifiers(HotkeyModifiers modifiers) =>
        modifiers.ToString().Replace(" ", string.Empty);
}
