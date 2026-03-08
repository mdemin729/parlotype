using Parlotype.Core.Hotkeys;
using SharpHook.Data;

namespace Parlotype.Platform.Hotkeys;

/// <summary>
/// Maps between platform-agnostic key names (used in <see cref="HotkeyBinding"/>)
/// and SharpHook <see cref="KeyCode"/> values.
/// </summary>
internal static class KeyCodeMapper
{
    private static readonly Dictionary<string, KeyCode> NameToCode = new(StringComparer.OrdinalIgnoreCase)
    {
        ["Space"] = KeyCode.VcSpace,
        ["Enter"] = KeyCode.VcEnter,
        ["Tab"] = KeyCode.VcTab,
        ["Escape"] = KeyCode.VcEscape,
        ["Backspace"] = KeyCode.VcBackspace,
        ["Delete"] = KeyCode.VcDelete,
        ["Insert"] = KeyCode.VcInsert,
        ["Home"] = KeyCode.VcHome,
        ["End"] = KeyCode.VcEnd,
        ["PageUp"] = KeyCode.VcPageUp,
        ["PageDown"] = KeyCode.VcPageDown,
        ["Up"] = KeyCode.VcUp,
        ["Down"] = KeyCode.VcDown,
        ["Left"] = KeyCode.VcLeft,
        ["Right"] = KeyCode.VcRight,
        ["PrintScreen"] = KeyCode.VcPrintScreen,
        ["Pause"] = KeyCode.VcPause,

        // Function keys
        ["F1"] = KeyCode.VcF1, ["F2"] = KeyCode.VcF2, ["F3"] = KeyCode.VcF3,
        ["F4"] = KeyCode.VcF4, ["F5"] = KeyCode.VcF5, ["F6"] = KeyCode.VcF6,
        ["F7"] = KeyCode.VcF7, ["F8"] = KeyCode.VcF8, ["F9"] = KeyCode.VcF9,
        ["F10"] = KeyCode.VcF10, ["F11"] = KeyCode.VcF11, ["F12"] = KeyCode.VcF12,

        // Letters
        ["A"] = KeyCode.VcA, ["B"] = KeyCode.VcB, ["C"] = KeyCode.VcC,
        ["D"] = KeyCode.VcD, ["E"] = KeyCode.VcE, ["F"] = KeyCode.VcF,
        ["G"] = KeyCode.VcG, ["H"] = KeyCode.VcH, ["I"] = KeyCode.VcI,
        ["J"] = KeyCode.VcJ, ["K"] = KeyCode.VcK, ["L"] = KeyCode.VcL,
        ["M"] = KeyCode.VcM, ["N"] = KeyCode.VcN, ["O"] = KeyCode.VcO,
        ["P"] = KeyCode.VcP, ["Q"] = KeyCode.VcQ, ["R"] = KeyCode.VcR,
        ["S"] = KeyCode.VcS, ["T"] = KeyCode.VcT, ["U"] = KeyCode.VcU,
        ["V"] = KeyCode.VcV, ["W"] = KeyCode.VcW, ["X"] = KeyCode.VcX,
        ["Y"] = KeyCode.VcY, ["Z"] = KeyCode.VcZ,

        // Digits
        ["0"] = KeyCode.Vc0, ["1"] = KeyCode.Vc1, ["2"] = KeyCode.Vc2,
        ["3"] = KeyCode.Vc3, ["4"] = KeyCode.Vc4, ["5"] = KeyCode.Vc5,
        ["6"] = KeyCode.Vc6, ["7"] = KeyCode.Vc7, ["8"] = KeyCode.Vc8,
        ["9"] = KeyCode.Vc9,

        // Punctuation
        ["Minus"] = KeyCode.VcMinus,
        ["Equals"] = KeyCode.VcEquals,
        ["BackQuote"] = KeyCode.VcBackQuote,
        ["Comma"] = KeyCode.VcComma,
        ["Period"] = KeyCode.VcPeriod,
        ["Slash"] = KeyCode.VcSlash,
        ["Semicolon"] = KeyCode.VcSemicolon,
        ["Quote"] = KeyCode.VcQuote,
        ["Backslash"] = KeyCode.VcBackslash,
    };

    private static readonly Dictionary<KeyCode, string> CodeToName =
        NameToCode.ToDictionary(kvp => kvp.Value, kvp => kvp.Key);

    /// <summary>Maps a key name to a SharpHook KeyCode. Returns null if not recognized.</summary>
    public static KeyCode? ToKeyCode(string keyName)
        => NameToCode.TryGetValue(keyName, out var code) ? code : null;

    /// <summary>Maps a SharpHook KeyCode to a key name. Returns null if not mapped.</summary>
    public static string? ToKeyName(KeyCode code)
        => CodeToName.TryGetValue(code, out var name) ? name : null;

    /// <summary>Returns true if the given KeyCode represents a modifier key.</summary>
    public static bool IsModifierKey(KeyCode code) => code is
        KeyCode.VcLeftControl or KeyCode.VcRightControl or
        KeyCode.VcLeftAlt or KeyCode.VcRightAlt or
        KeyCode.VcLeftShift or KeyCode.VcRightShift or
        KeyCode.VcLeftMeta or KeyCode.VcRightMeta;

    /// <summary>Extracts <see cref="HotkeyModifiers"/> from a SharpHook <see cref="EventMask"/>.</summary>
    public static HotkeyModifiers ToHotkeyModifiers(EventMask mask)
    {
        var mods = HotkeyModifiers.None;

        if ((mask & EventMask.Ctrl) != 0)
            mods |= HotkeyModifiers.Ctrl;
        if ((mask & EventMask.Alt) != 0)
            mods |= HotkeyModifiers.Alt;
        if ((mask & EventMask.Shift) != 0)
            mods |= HotkeyModifiers.Shift;
        if ((mask & EventMask.Meta) != 0)
            mods |= HotkeyModifiers.Meta;

        return mods;
    }
}
