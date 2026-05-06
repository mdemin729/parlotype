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

    public RuntimeUnavailableException(RuntimePreference requested, string reason)
        : base($"Whisper runtime '{requested}' is not available: {reason}")
    {
        Requested = requested;
        Reason = reason;
    }

    public RuntimeUnavailableException(RuntimePreference requested, string reason, Exception inner)
        : base($"Whisper runtime '{requested}' is not available: {reason}", inner)
    {
        Requested = requested;
        Reason = reason;
    }
}
