using System.Text.RegularExpressions;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Post-processes transcription text by optionally stripping sentence punctuation
/// and/or masking profanity. Loaded once, reused across transcriptions.
/// </summary>
public sealed partial class TranscriptionTextProcessor
{
    private readonly bool _stripPunctuation;
    private readonly bool _filterProfanity;
    private readonly HashSet<string> _profanityWords;

    /// <summary>
    /// Matches sentence-level punctuation (period, comma, semicolon, colon,
    /// exclamation/question marks, ellipsis, em/en dashes) while preserving
    /// intra-word punctuation (apostrophes in "don't", hyphens in "co-op",
    /// decimals in "3.14").
    /// </summary>
    [GeneratedRegex(
        @"\.{2,}|(?<=\s|^)[.!?,;:\u2014\u2013]|[.!?,;:\u2014\u2013](?=\s|$)|\u2026",
        RegexOptions.Compiled)]
    private static partial Regex SentencePunctuationRegex();

    /// <summary>
    /// Matches whole words only for profanity filtering (case-insensitive).
    /// Built dynamically from the word list.
    /// </summary>
    private readonly Regex? _profanityRegex;

    public TranscriptionTextProcessor(bool stripPunctuation, bool filterProfanity)
    {
        _stripPunctuation = stripPunctuation;
        _filterProfanity = filterProfanity;
        _profanityWords = LoadProfanityWords();

        if (_filterProfanity && _profanityWords.Count > 0)
        {
            var escaped = _profanityWords.Select(Regex.Escape);
            var pattern = @"\b(" + string.Join("|", escaped) + @")\b";
            _profanityRegex = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Compiled);
        }
    }

    /// <summary>Applies configured post-processing to transcription text.</summary>
    public string Process(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        if (_stripPunctuation)
            text = SentencePunctuationRegex().Replace(text, "");

        if (_filterProfanity && _profanityRegex is not null)
            text = _profanityRegex.Replace(text, match => new string('*', match.Length));

        // Collapse multiple spaces that may result from punctuation removal
        text = MultipleSpacesRegex().Replace(text, " ").Trim();

        return text;
    }

    [GeneratedRegex(@" {2,}", RegexOptions.Compiled)]
    private static partial Regex MultipleSpacesRegex();

    private static HashSet<string> LoadProfanityWords()
    {
        var assembly = typeof(TranscriptionTextProcessor).Assembly;
        var resourceName = "Parlotype.Platform.Resources.profanity-words.txt";

        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
            return [];

        using var reader = new StreamReader(stream);
        var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (trimmed.Length > 0 && !trimmed.StartsWith('#'))
                words.Add(trimmed);
        }

        return words;
    }
}
