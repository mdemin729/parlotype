namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Timing thresholds that separate a deliberate gesture from ordinary typing.
/// </summary>
public static class HotkeyGestureTiming
{
    /// <summary>
    /// A press/release pair counts as a "tap" only if it completes within this
    /// window and no other key went down in between. Without the second
    /// condition, Ctrl+C followed quickly by Ctrl+V reads as a double-tap.
    /// </summary>
    public const int TapMaxMs = 250;

    /// <summary>Maximum gap between the two taps of a double-tap.</summary>
    public const int DoubleTapWindowMs = 350;

    /// <summary>
    /// A hold that sees another key this soon after starting was a keyboard
    /// shortcut, not a dictation gesture — Right Ctrl+C, say — so the recording
    /// it began is discarded. Later keypresses are ignored, since users do type
    /// while dictating.
    /// </summary>
    public const int HoldAbortGraceMs = 300;
}
