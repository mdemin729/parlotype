namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Detects conflicts between user-configured hotkeys and reserved OS shortcuts.
/// </summary>
public static class HotkeyConflictDetector
{
    private static readonly (HotkeyModifiers Modifiers, string Key, string Description)[] ReservedShortcuts =
    [
        // Windows reserved
        (HotkeyModifiers.Meta, "L", "Lock workstation"),
        (HotkeyModifiers.Meta, "E", "Open File Explorer"),
        (HotkeyModifiers.Meta, "R", "Open Run dialog"),
        (HotkeyModifiers.Meta, "D", "Show/hide desktop"),
        (HotkeyModifiers.Meta, "I", "Open Settings"),
        (HotkeyModifiers.Meta, "Tab", "Task View"),
        (HotkeyModifiers.Meta, "P", "Project display"),
        (HotkeyModifiers.Meta, "X", "Quick Link menu"),
        (HotkeyModifiers.Meta, "G", "Open Game Bar"),
        (HotkeyModifiers.Meta, "PrintScreen", "Screenshot"),

        // Ctrl+Alt+Delete is handled at kernel level
        (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Delete", "Security screen"),

        // macOS reserved (also flagged on all platforms for safety)
        (HotkeyModifiers.Meta, "Q", "Quit application (macOS)"),
        (HotkeyModifiers.Meta, "W", "Close window (macOS)"),
        (HotkeyModifiers.Meta, "Space", "Spotlight search (macOS)"),
        (HotkeyModifiers.Meta, "H", "Hide window (macOS)"),
    ];

    /// <summary>Returns true if the binding conflicts with a known reserved shortcut.</summary>
    public static bool IsReserved(HotkeyBinding binding)
        => GetConflictDescription(binding) is not null;

    /// <summary>
    /// Returns a human-readable description of the conflict, or null if no conflict exists.
    /// </summary>
    public static string? GetConflictDescription(HotkeyBinding binding)
    {
        foreach (var (modifiers, key, description) in ReservedShortcuts)
        {
            if (binding.Modifiers == modifiers &&
                string.Equals(binding.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                return $"{binding.DisplayString} is reserved: {description}";
            }
        }

        return null;
    }
}
