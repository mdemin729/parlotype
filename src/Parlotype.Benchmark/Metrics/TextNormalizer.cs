using System.Text.RegularExpressions;

namespace Parlotype.Benchmark.Metrics;

/// <summary>Normalizes text for fair WER/CER comparison between reference and hypothesis.</summary>
public static partial class TextNormalizer
{
    /// <summary>
    /// Normalizes text: lowercase, remove punctuation, collapse whitespace, trim.
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var result = text.ToLowerInvariant();
        result = PunctuationRegex().Replace(result, " ");
        result = WhitespaceRegex().Replace(result, " ");
        return result.Trim();
    }

    [GeneratedRegex(@"[^\p{L}\p{N}\s]")]
    private static partial Regex PunctuationRegex();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();
}
