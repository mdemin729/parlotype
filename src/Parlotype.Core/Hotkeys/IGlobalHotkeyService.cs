namespace Parlotype.Core.Hotkeys;

/// <summary>
/// Listens for global dictation gestures even when the app is not focused, and
/// reports them as intent rather than as raw key events.
/// </summary>
public interface IGlobalHotkeyService : IDisposable
{
    /// <summary>Starts listening for global hotkeys.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Stops listening for global hotkeys.</summary>
    Task StopAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// A gesture asked for dictation to begin. The args say whether the gesture is
    /// hold-scoped, which decides whether silence may end the utterance (ADR-060).
    /// </summary>
    event EventHandler<DictationStartEventArgs> DictationStartRequested;

    /// <summary>A gesture asked for dictation to finish and transcribe.</summary>
    event EventHandler DictationStopRequested;

    /// <summary>
    /// Dictation should stop and the captured audio be thrown away — the user
    /// pressed Escape, or a hold turned out to be a keyboard shortcut.
    /// </summary>
    event EventHandler DictationCancelRequested;

    /// <summary>Raised after <see cref="UpdateBindings"/> so the UI can restate the current gesture.</summary>
    event EventHandler BindingsChanged;

    /// <summary>The currently configured bindings.</summary>
    IReadOnlyList<DictationHotkey> Bindings { get; }

    /// <summary>Replaces the binding set at runtime and persists it.</summary>
    void UpdateBindings(IReadOnlyList<DictationHotkey> bindings);

    /// <summary>
    /// Tells the service whether dictation is currently running. Toggle gestures
    /// and Escape both depend on it, and recording can start or stop by means the
    /// service never sees — the widget's button, a model that failed to load — so
    /// the owner reports the truth instead of the service inferring it.
    /// </summary>
    void SetDictationActive(bool active);
}
