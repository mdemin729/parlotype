namespace Parlotype.Core.Settings;

/// <summary>Well-known keys for <see cref="IWindowStateService"/>.</summary>
public static class WindowStateKeys
{
    /// <summary>
    /// Last user-chosen Transcribe window position (<see cref="WindowPosition"/>).
    /// The window is frameless and positioned by dragging its grip strip, so the
    /// position must survive restarts. Restored only when the point is still on
    /// a connected screen (ADR-040).
    /// </summary>
    public const string TranscribeWindowPosition = "TranscribeWindowPosition";
}
