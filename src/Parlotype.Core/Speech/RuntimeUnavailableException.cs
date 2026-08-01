namespace Parlotype.Core.Speech;

/// <summary>
/// Thrown when a strict (non-Auto) <see cref="RuntimePreference"/> is requested
/// but the corresponding Whisper.net runtime backend is not usable on this
/// machine. The application surfaces this to the user instead of silently
/// falling back to CPU.
/// </summary>
public sealed class RuntimeUnavailableException : Exception
{
    public RuntimePreference Requested { get; }
    public string Reason { get; }

    /// <summary>
    /// True when the requested runtime is fine on this machine but a different
    /// one is already loaded into the current process. Runtime selection is
    /// process-wide and one-shot, so only an app restart can apply the change.
    /// </summary>
    public bool RequiresRestart { get; }

    public RuntimeUnavailableException(RuntimePreference requested, string reason, bool requiresRestart = false)
        : base($"Whisper runtime '{requested}' is not available: {reason}")
    {
        Requested = requested;
        Reason = reason;
        RequiresRestart = requiresRestart;
    }

    public RuntimeUnavailableException(RuntimePreference requested, string reason, Exception inner, bool requiresRestart = false)
        : base($"Whisper runtime '{requested}' is not available: {reason}", inner)
    {
        Requested = requested;
        Reason = reason;
        RequiresRestart = requiresRestart;
    }
}
