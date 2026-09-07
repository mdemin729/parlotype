namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Validates a candidate hotkey against shortcuts the OS has already claimed,
/// against combinations that commonly collide with other applications, and
/// against the user's other Parlotype bindings.
/// </summary>
/// <remarks>
/// Every outcome carries a <see cref="HotkeyConflictReason"/> so the UI can
/// build the sentence in the user's language; the English text produced here is
/// the invariant form the log lines write (ADR-064 amendment).
/// </remarks>
public static class HotkeyConflictDetector
{
    private static readonly (HotkeyModifiers Modifiers, string Key, ReservedShortcut Shortcut)[] ReservedShortcuts =
    [
        // Windows reserved
        (HotkeyModifiers.Meta, "L", ReservedShortcut.LockWorkstation),
        (HotkeyModifiers.Meta, "E", ReservedShortcut.FileExplorer),
        (HotkeyModifiers.Meta, "R", ReservedShortcut.RunDialog),
        (HotkeyModifiers.Meta, "D", ReservedShortcut.ShowDesktop),
        (HotkeyModifiers.Meta, "I", ReservedShortcut.WindowsSettings),
        (HotkeyModifiers.Meta, "Tab", ReservedShortcut.TaskView),
        (HotkeyModifiers.Meta, "P", ReservedShortcut.ProjectDisplay),
        (HotkeyModifiers.Meta, "X", ReservedShortcut.QuickLinkMenu),
        (HotkeyModifiers.Meta, "G", ReservedShortcut.GameBar),
        (HotkeyModifiers.Meta, "PrintScreen", ReservedShortcut.Screenshot),

        // Ctrl+Alt+Delete is handled at kernel level
        (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Delete", ReservedShortcut.SecurityScreen),

        // Other dictation tools — the shell claims these before any hook sees them
        (HotkeyModifiers.Meta | HotkeyModifiers.Ctrl, "S", ReservedShortcut.WindowsSpeechRecognition),

        // Input-source switching; a multilingual app should not fight the layout switcher
        (HotkeyModifiers.Meta | HotkeyModifiers.Ctrl, "Space", ReservedShortcut.SwitchInputSource),

        // macOS reserved (also flagged on all platforms for safety)
        (HotkeyModifiers.Meta, "Q", ReservedShortcut.MacQuit),
        (HotkeyModifiers.Meta, "W", ReservedShortcut.MacCloseWindow),
        (HotkeyModifiers.Meta, "Space", ReservedShortcut.Spotlight),
        (HotkeyModifiers.Meta, "H", ReservedShortcut.VoiceTyping),
    ];

    /// <summary>Returns true if the binding conflicts with a known reserved shortcut.</summary>
    public static bool IsReserved(HotkeyBinding binding)
        => TryGetReserved(binding, out _);

    /// <summary>
    /// Identifies the reserved shortcut a binding collides with, if any. This is
    /// the form the UI wants: an identity it can translate, not a sentence.
    /// </summary>
    public static bool TryGetReserved(HotkeyBinding binding, out ReservedShortcut shortcut)
    {
        foreach (var (modifiers, key, reserved) in ReservedShortcuts)
        {
            if (binding.Modifiers == modifiers &&
                string.Equals(binding.Key, key, StringComparison.OrdinalIgnoreCase))
            {
                shortcut = reserved;
                return true;
            }
        }

        shortcut = default;
        return false;
    }

    /// <summary>
    /// What a reserved shortcut does, in invariant English. Used inside log
    /// lines and the invariant <see cref="HotkeyConflict.Description"/>; the
    /// window shows the translated form instead.
    /// </summary>
    public static string Describe(ReservedShortcut shortcut) => shortcut switch
    {
        ReservedShortcut.LockWorkstation => "Lock workstation",
        ReservedShortcut.FileExplorer => "Open File Explorer",
        ReservedShortcut.RunDialog => "Open Run dialog",
        ReservedShortcut.ShowDesktop => "Show/hide desktop",
        ReservedShortcut.WindowsSettings => "Open Settings",
        ReservedShortcut.TaskView => "Task View",
        ReservedShortcut.ProjectDisplay => "Project display",
        ReservedShortcut.QuickLinkMenu => "Quick Link menu",
        ReservedShortcut.GameBar => "Open Game Bar",
        ReservedShortcut.Screenshot => "Screenshot",
        ReservedShortcut.SecurityScreen => "Security screen",
        ReservedShortcut.WindowsSpeechRecognition => "Windows Speech Recognition",
        ReservedShortcut.SwitchInputSource => "Switch input source",
        ReservedShortcut.MacQuit => "Quit application (macOS)",
        ReservedShortcut.MacCloseWindow => "Close window (macOS)",
        ReservedShortcut.Spotlight => "Spotlight search (macOS) / input source switching",
        ReservedShortcut.VoiceTyping => "Windows Voice Typing; hide window on macOS and GNOME",
        _ => shortcut.ToString(),
    };

    /// <summary>
    /// Returns a human-readable description of the conflict, or null if no conflict exists.
    /// </summary>
    public static string? GetConflictDescription(HotkeyBinding binding)
        => TryGetReserved(binding, out var shortcut)
            ? $"{binding.DisplayString} is reserved: {Describe(shortcut)}"
            : null;

    /// <summary>
    /// Validates a candidate binding against the OS, against common application
    /// shortcuts, and against the bindings already configured.
    /// </summary>
    public static HotkeyConflict Check(
        DictationHotkey candidate,
        IReadOnlyList<DictationHotkey>? existing = null)
    {
        if (!candidate.IsValid)
            return HotkeyConflict.Invalid("That combination can't be used as a dictation hotkey.");

        if (existing is not null)
        {
            foreach (var other in existing)
            {
                if (!Overlaps(candidate.Gesture, other.Gesture))
                    continue;

                return HotkeyConflict.AlreadyBound(
                    $"{candidate.DisplayString} is already bound to {other.ModeLabel.ToLowerInvariant()}.",
                    other.Mode);
            }
        }

        if (candidate.Gesture.Kind != HotkeyGestureKind.Chord || candidate.Gesture.Chord is not { } chord)
            return HotkeyConflict.None;

        if (TryGetReserved(chord, out var reserved))
        {
            return HotkeyConflict.ReservedBy(
                $"{chord.DisplayString} is reserved: {Describe(reserved)}",
                reserved);
        }

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
                $"{chord.DisplayString} shows parameter hints in Visual Studio and VS Code.",
                HotkeyConflictReason.ParameterHints);
        }

        // AltGr reports as Ctrl+Alt, so on European layouts these fire while the
        // user is typing accented characters. Space is safe — it produces no character.
        if (chord.Modifiers == (HotkeyModifiers.Ctrl | HotkeyModifiers.Alt) &&
            chord.Key.Length == 1 &&
            char.IsLetterOrDigit(chord.Key[0]))
        {
            return HotkeyConflict.Warning(
                $"On European keyboard layouts AltGr acts as Ctrl+Alt, so {chord.DisplayString} " +
                "can fire while typing accented characters. Ctrl+Alt+Space avoids this.",
                HotkeyConflictReason.AltGrCollision);
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
