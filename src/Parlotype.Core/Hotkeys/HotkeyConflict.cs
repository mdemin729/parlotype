namespace Parlotype.Core.Hotkeys;

/// <summary>How much trouble a candidate binding is in.</summary>
public enum HotkeyConflictSeverity
{
    None,

    /// <summary>Usable, but likely to surprise the user — shown as a caution, not a block.</summary>
    Warning,

    /// <summary>Taken by the OS or by another Parlotype binding; must not be accepted.</summary>
    Blocking
}

/// <summary>The outcome of validating a candidate binding.</summary>
public readonly record struct HotkeyConflict(HotkeyConflictSeverity Severity, string? Description)
{
    public static HotkeyConflict None { get; } = new(HotkeyConflictSeverity.None, null);

    public static HotkeyConflict Blocking(string description) =>
        new(HotkeyConflictSeverity.Blocking, description);

    public static HotkeyConflict Warning(string description) =>
        new(HotkeyConflictSeverity.Warning, description);

    /// <summary>True when the binding must be rejected rather than merely flagged.</summary>
    public bool IsBlocking => Severity == HotkeyConflictSeverity.Blocking;

    public bool HasMessage => !string.IsNullOrEmpty(Description);
}
