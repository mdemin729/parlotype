namespace Parlotype.Core.TextInjection;

/// <summary>
/// Tracks the last non-Parlotype foreground window so text can be
/// injected into the correct application.
/// </summary>
public interface ITargetWindowTracker : IDisposable
{
    /// <summary>Handle of the last foreground window that belongs to another process.</summary>
    nint? TargetWindow { get; }

    /// <summary>Activates the tracked target window, returning true on success.</summary>
    bool ActivateTargetWindow();
}
