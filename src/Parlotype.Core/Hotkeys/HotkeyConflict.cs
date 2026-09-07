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

/// <summary>
/// Why a candidate binding was flagged. The UI switches on this to build a
/// localized sentence (ADR-064 amendment); <see cref="HotkeyConflict.Description"/>
/// stays the invariant English form the log lines write.
/// </summary>
public enum HotkeyConflictReason
{
    None,

    /// <summary>The gesture and activation mode cannot be combined at all.</summary>
    InvalidCombination,

    /// <summary>Another configured binding already competes for the same keystrokes.</summary>
    AlreadyBound,

    /// <summary>The OS or another dictation tool has claimed the chord.</summary>
    Reserved,

    /// <summary>Ctrl+Shift+Space is the parameter-hint shortcut in Visual Studio and VS Code.</summary>
    ParameterHints,

    /// <summary>AltGr reports as Ctrl+Alt, so the chord fires while typing accented characters.</summary>
    AltGrCollision
}

/// <summary>
/// The shortcuts <see cref="HotkeyConflictDetector"/> refuses to hand over, as
/// identities rather than sentences. Core names them; Desktop translates them.
/// </summary>
public enum ReservedShortcut
{
    LockWorkstation,
    FileExplorer,
    RunDialog,
    ShowDesktop,
    WindowsSettings,
    TaskView,
    ProjectDisplay,
    QuickLinkMenu,
    GameBar,
    Screenshot,
    SecurityScreen,
    WindowsSpeechRecognition,
    SwitchInputSource,
    MacQuit,
    MacCloseWindow,
    Spotlight,
    VoiceTyping
}

/// <summary>
/// The outcome of validating a candidate binding. <see cref="Reason"/> and the
/// payload beside it are what the UI formats; <see cref="Description"/> is the
/// invariant English form, kept for logs and for tests that assert on wording.
/// </summary>
public readonly record struct HotkeyConflict(
    HotkeyConflictSeverity Severity,
    string? Description,
    HotkeyConflictReason Reason = HotkeyConflictReason.None,
    ReservedShortcut? Reserved = null,
    ActivationMode? ConflictingMode = null)
{
    public static HotkeyConflict None { get; } = new(HotkeyConflictSeverity.None, null);

    public static HotkeyConflict Invalid(string description) =>
        new(HotkeyConflictSeverity.Blocking, description, HotkeyConflictReason.InvalidCombination);

    public static HotkeyConflict AlreadyBound(string description, ActivationMode conflictingMode) =>
        new(HotkeyConflictSeverity.Blocking, description, HotkeyConflictReason.AlreadyBound,
            ConflictingMode: conflictingMode);

    public static HotkeyConflict ReservedBy(string description, ReservedShortcut shortcut) =>
        new(HotkeyConflictSeverity.Blocking, description, HotkeyConflictReason.Reserved,
            Reserved: shortcut);

    public static HotkeyConflict Warning(string description, HotkeyConflictReason reason) =>
        new(HotkeyConflictSeverity.Warning, description, reason);

    /// <summary>True when the binding must be rejected rather than merely flagged.</summary>
    public bool IsBlocking => Severity == HotkeyConflictSeverity.Blocking;

    public bool HasMessage => !string.IsNullOrEmpty(Description);
}
