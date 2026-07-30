namespace Parlotype.Core.Hotkeys;

/// <summary>
/// A single modifier key, used as the subject of a bare-modifier gesture
/// (hold or double-tap). Distinct from <see cref="HotkeyModifiers"/>, which is
/// a flags set describing the modifiers held alongside a chord's primary key.
/// </summary>
public enum ModifierKey
{
    Ctrl,
    Alt,
    Shift,
    Meta
}

/// <summary>
/// Which physical instance of a modifier key a gesture listens to. SharpHook
/// reports left and right modifiers as distinct key codes
/// (<c>VcLeftControl</c> / <c>VcRightControl</c>), which is what lets
/// "Hold Right Ctrl" coexist with every normal left-Ctrl shortcut.
/// </summary>
public enum ModifierSide
{
    /// <summary>Either physical key. Double-taps must still land on the same one.</summary>
    Either,
    Left,
    Right
}
