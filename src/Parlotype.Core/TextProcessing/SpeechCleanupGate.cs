namespace Parlotype.Core.TextProcessing;

/// <summary>
/// Decides whether a transcript should be sent to the cleanup model, and whether
/// what came back can be trusted (ADR-073).
/// </summary>
/// <remarks>
/// The cleanup model is English-only and small. Handing it text it was not
/// trained on, or accepting whatever it returns, would replace what the user
/// said with something they did not say. Both checks err towards the raw
/// transcript: a missed cleanup costs polish, a wrong one costs the dictation.
/// </remarks>
public static class SpeechCleanupGate
{
    /// <summary>
    /// Longest transcript sent for cleanup. Beyond it the input plus the
    /// rewritten output no longer fits the context the server is started with.
    /// </summary>
    public const int MaxTranscriptChars = 6000;

    /// <summary>
    /// Share of non-ASCII letters that English text stays under without help:
    /// the odd "café" in a long sentence.
    /// </summary>
    private const double PlainNonAsciiLetterShare = 0.03;

    /// <summary>
    /// Share of non-ASCII letters tolerated when the words themselves say the
    /// text is English: a short sentence around a name like "Müller". Above it
    /// the text is in another script or another language.
    /// </summary>
    private const double MaxNonAsciiLetterShare = 0.10;

    /// <summary>
    /// True when <paramref name="transcript"/> is worth sending and nothing but
    /// the text is known about its language: non-empty, short enough, and
    /// plausibly English.
    /// </summary>
    public static bool ShouldClean(string? transcript) =>
        Evaluate(transcript, CleanupLanguage.Unknown) is null;

    /// <summary>
    /// Decides whether <paramref name="transcript"/> goes to the model, given
    /// what is known about its language (ADR-075). Returns null to send it, or
    /// the outcome to report for leaving it as spoken.
    /// </summary>
    /// <param name="transcript">The recognized text.</param>
    /// <param name="language">
    /// The output language from <see cref="CleanupLanguagePolicy"/>. Known
    /// English is trusted and the text is not second-guessed; a known other
    /// language is skipped; only when it is unknown is the text itself examined.
    /// </param>
    public static SpeechCleanupOutcome? Evaluate(string? transcript, CleanupLanguage language)
    {
        if (string.IsNullOrWhiteSpace(transcript))
            return SpeechCleanupOutcome.NotRun;

        if (language.Kind == CleanupLanguageKind.NotEnglish)
            return SpeechCleanupOutcome.SkippedNotEnglish;

        if (transcript.Length > MaxTranscriptChars)
            return SpeechCleanupOutcome.SkippedTooLong;

        if (language.Kind == CleanupLanguageKind.Unknown && !IsLikelyEnglish(transcript))
        {
            // Digits and punctuation alone: nothing was skipped, there is nothing to clean.
            return transcript.Any(char.IsLetter)
                ? SpeechCleanupOutcome.SkippedNotEnglish
                : SpeechCleanupOutcome.NotRun;
        }

        return null;
    }

    /// <summary>
    /// A cheap test on the text alone, not language identification. It rejects
    /// text in another script (Cyrillic, Greek), text dense with diacritics, and
    /// text whose function words belong to another language at least as much as
    /// to English (<see cref="EnglishTextEvidence"/>). It errs towards English
    /// when there is no evidence either way: a short reply in another
    /// Latin-script language with no diacritics still passes.
    /// </summary>
    public static bool IsLikelyEnglish(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var letters = 0;
        var nonAscii = 0;
        foreach (var ch in text)
        {
            if (!char.IsLetter(ch))
                continue;

            letters++;
            if (ch > 0x7F)
                nonAscii++;
        }

        if (letters == 0 || nonAscii > letters * MaxNonAsciiLetterShare)
            return false;

        var (english, foreign) = EnglishTextEvidence.Score(text);
        if (foreign > 0 && foreign >= english)
            return false;

        // Accented letters beyond the odd one need the words to vouch for them.
        if (nonAscii > letters * PlainNonAsciiLetterShare)
            return english > 0 && foreign == 0;

        return true;
    }

    /// <summary>
    /// True when <paramref name="cleaned"/> is a believable rewrite of
    /// <paramref name="raw"/>. Cleanup removes words and adds punctuation, so the
    /// result may be much shorter, even empty, but never much longer. Output that
    /// outgrows its input is the model looping or inventing text.
    /// </summary>
    public static bool IsPlausibleResult(string raw, string cleaned)
    {
        ArgumentNullException.ThrowIfNull(raw);
        ArgumentNullException.ThrowIfNull(cleaned);

        // Written forms can be longer than spoken ones (list bullets, an e-mail
        // layout's blank lines), hence the slack on short inputs.
        return cleaned.Length <= raw.Length * 1.5 + 40;
    }
}
