namespace Parlotype.Core.TextInjection;

/// <summary>
/// The operating system would not let Parlotype put the recognized text into
/// the target application: on macOS, the Accessibility permission is missing,
/// so the paste keystroke is refused (ADR-077).
/// </summary>
/// <remarks>
/// Distinct from an ordinary injection failure because the dictation is not
/// lost and the remedy is the user's: an implementation that throws this has
/// left the text on the clipboard, and the caller should say so and point at
/// the system setting, rather than log an error nobody reads.
/// </remarks>
public sealed class TextInjectionNotPermittedException : InvalidOperationException
{
    public TextInjectionNotPermittedException()
        : base("The operating system did not permit pasting into the target application. " +
               "The text was left on the clipboard.")
    {
    }
}
