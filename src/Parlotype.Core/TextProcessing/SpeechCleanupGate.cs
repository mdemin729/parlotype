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

    /// <summary>Share of non-ASCII letters above which text is not treated as English.</summary>
    private const double MaxNonAsciiLetterShare = 0.03;

    /// <summary>
    /// True when <paramref name="transcript"/> is worth sending: non-empty, short
    /// enough, and plausibly English.
    /// </summary>
    public static bool ShouldClean(string? transcript) =>
        !string.IsNullOrWhiteSpace(transcript)
        && transcript.Length <= MaxTranscriptChars
        && IsLikelyEnglish(transcript);

    /// <summary>
    /// A cheap script test, not language identification. English is written in
    /// unaccented Latin letters, so text in another script (Cyrillic, Greek) or
    /// dense with diacritics (most other European languages) fails it. A Latin
    /// sentence that happens to carry no diacritics passes, whatever its language.
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

        return letters > 0 && nonAscii <= letters * MaxNonAsciiLetterShare;
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
