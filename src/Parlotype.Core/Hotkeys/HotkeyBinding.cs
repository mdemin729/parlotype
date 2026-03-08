using System.Text;

namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Represents a keyboard shortcut combination (modifier keys + primary key).
/// Key names use platform-agnostic identifiers (e.g. "Space", "F1", "A").
/// </summary>
public sealed record HotkeyBinding(HotkeyModifiers Modifiers, string Key)
{
    /// <summary>Default hotkey: Ctrl+Shift+Space.</summary>
    public static HotkeyBinding Default { get; } = new(HotkeyModifiers.Ctrl | HotkeyModifiers.Shift, "Space");

    /// <summary>Human-readable display string (e.g. "Ctrl+Shift+Space").</summary>
    public string DisplayString
    {
        get
        {
            var sb = new StringBuilder();

            if (Modifiers.HasFlag(HotkeyModifiers.Ctrl))
                sb.Append("Ctrl+");
            if (Modifiers.HasFlag(HotkeyModifiers.Alt))
                sb.Append("Alt+");
            if (Modifiers.HasFlag(HotkeyModifiers.Shift))
                sb.Append("Shift+");
            if (Modifiers.HasFlag(HotkeyModifiers.Meta))
                sb.Append("Win+");

            sb.Append(Key);
            return sb.ToString();
        }
    }

    /// <summary>Returns true if this binding has at least one modifier and a non-empty key.</summary>
    public bool IsValid => Modifiers != HotkeyModifiers.None && !string.IsNullOrWhiteSpace(Key);
}
