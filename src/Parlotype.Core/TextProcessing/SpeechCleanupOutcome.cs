namespace Parlotype.Core.TextProcessing;

/// <summary>
/// What speech cleanup did with one transcript (ADR-075). Carried on the
/// <see cref="Speech.TranscriptionResult"/> so the UI can say why a dictation
/// was pasted as spoken; before this the reason was only in the log.
/// </summary>
public enum SpeechCleanupOutcome
{
    /// <summary>Cleanup is off, or there was no text to clean.</summary>
    NotRun,

    /// <summary>The text was rewritten by the cleanup model.</summary>
    Cleaned,

    /// <summary>Left as spoken: the text is not English, by the settings, the recognizer or the text itself.</summary>
    SkippedNotEnglish,

    /// <summary>Left as spoken: too long for the model's context.</summary>
    SkippedTooLong,

    /// <summary>Left as spoken: the model failed, timed out, or returned something implausible.</summary>
    Failed,
}
