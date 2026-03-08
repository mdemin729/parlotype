namespace Parlotype.Core.Hotkeys;

/// <summary>Modifier keys for hotkey combinations.</summary>
[Flags]
public enum HotkeyModifiers
{
    None = 0,
    Ctrl = 1,
    Alt = 2,
    Shift = 4,
    Meta = 8
}
