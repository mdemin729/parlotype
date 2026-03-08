namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Listens for global keyboard shortcuts even when the app is not focused.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>Starts listening for global hotkeys.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops listening for global hotkeys.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>Raised when the push-to-talk hotkey is pressed.</summary>
    event EventHandler HotkeyPressed;

    /// <summary>Raised when the push-to-talk hotkey is released.</summary>
    event EventHandler HotkeyReleased;

    /// <summary>The currently configured hotkey binding.</summary>
    HotkeyBinding CurrentBinding { get; }

    /// <summary>Updates the hotkey binding at runtime.</summary>
    void UpdateBinding(HotkeyBinding binding);

    /// <summary>Current activation mode (PTT or Toggle).</summary>
    ActivationMode Mode { get; set; }
}
