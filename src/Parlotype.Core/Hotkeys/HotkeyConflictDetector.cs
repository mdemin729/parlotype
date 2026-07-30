namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Validates a candidate hotkey against shortcuts the OS has already claimed,
/// against combinations that commonly collide with other applications, and
/// against the user's other Parlotype bindings.
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

        // Other dictation tools — the shell claims these before any hook sees them
        (HotkeyModifiers.Meta | HotkeyModifiers.Ctrl, "S", "Windows Speech Recognition"),

        // Input-source switching; a multilingual app should not fight the layout switcher
        (HotkeyModifiers.Meta | HotkeyModifiers.Ctrl, "Space", "Switch input source"),

        // macOS reserved (also flagged on all platforms for safety)
        (HotkeyModifiers.Meta, "Q", "Quit application (macOS)"),
        (HotkeyModifiers.Meta, "W", "Close window (macOS)"),
        (HotkeyModifiers.Meta, "Space", "Spotlight search (macOS) / input source switching"),
        (HotkeyModifiers.Meta, "H", "Windows Voice Typing; hide window on macOS and GNOME"),
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

    /// <summary>
    /// Validates a candidate binding against the OS, against common application
    /// shortcuts, and against the bindings already configured.
    /// </summary>
    public static HotkeyConflict Check(
        DictationHotkey candidate,
        IReadOnlyList<DictationHotkey>? existing = null)
    {
        if (!candidate.IsValid)
            return HotkeyConflict.Blocking("That combination can't be used as a dictation hotkey.");

        if (existing is not null)
        {
            foreach (var other in existing)
            {
                if (!Overlaps(candidate.Gesture, other.Gesture))
                    continue;

                return HotkeyConflict.Blocking(
                    $"{candidate.DisplayString} is already bound to {other.ModeLabel.ToLowerInvariant()}.");
            }
        }

        if (candidate.Gesture.Kind != HotkeyGestureKind.Chord || candidate.Gesture.Chord is not { } chord)
            return HotkeyConflict.None;

        if (GetConflictDescription(chord) is { } reserved)
            return HotkeyConflict.Blocking(reserved);

        return CheckApplicationCollisions(chord);
    }

    /// <summary>
    /// Chords that work, but tend to be taken. These are warnings rather than
    /// blocks: they depend on which apps and keyboard layout the user has.
    /// </summary>
    private static HotkeyConflict CheckApplicationCollisions(HotkeyBinding chord)
    {
        if (chord.Modifiers == (HotkeyModifiers.Ctrl | HotkeyModifiers.Shift) &&
            string.Equals(chord.Key, "Space", StringComparison.OrdinalIgnoreCase))
        {
            return HotkeyConflict.Warning(
                $"{chord.DisplayString} shows parameter hints in Visual Studio and VS Code.");
        }

        // AltGr reports as Ctrl+Alt, so on European layouts these fire while the
        // user is typing accented characters. Space is safe — it produces no character.
        if (chord.Modifiers == (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt) &&
            chord.Key.Length == 1 &&
            char.IsLetterOrDigit(chord.Key[0]))
        {
            return HotkeyConflict.Warning(
                $"On European keyboard layouts AltGr acts as Ctrl+Alt, so {chord.DisplayString} " +
                "can fire while typing accented characters. Ctrl+Alt+Space avoids this.");
        }

        return HotkeyConflict.None;
    }

    /// <summary>
    /// True when two gestures would compete for the same keystrokes. Hold and
    /// double-tap on the same modifier deliberately do not — that pairing is the
    /// shipped default, and the trackers tell them apart by timing.
    /// </summary>
    private static bool Overlaps(HotkeyGesture candidate, HotkeyGesture existing)
    {
        if (candidate.Kind != existing.Kind)
            return false;

        return candidate.Kind == HotkeyGestureKind.Chord
            ? candidate.Chord == existing.Chord
            : candidate.SharesModifierKeyWith(existing);
    }
}
