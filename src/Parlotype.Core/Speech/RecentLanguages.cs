namespace Parlotype.Core.Speech;

/// <summary>
/// Pure most-recently-used list logic for language codes. Kept free of storage
/// concerns so it is trivially testable; the persistence layer reads/writes the
/// list under <see cref="Settings.SettingsKeys.RecentSourceLanguages"/> or
/// <see cref="Settings.SettingsKeys.RecentTargetLanguages"/> depending on role.
/// </summary>
public static class RecentLanguages
{
    /// <summary>Default cap on the number of remembered languages.</summary>
    public const int DefaultMax = 5;

    /// <summary>
    /// Returns a new list with <paramref name="code"/> moved to the front,
    /// duplicates removed (case-insensitive), and the result capped at
    /// <paramref name="max"/>. The special sentinels
    /// (<see cref="LanguageCatalog.AutoDetectCode"/> /
    /// <see cref="LanguageCatalog.NoTranslationCode"/>) and blank codes are ignored.
    /// </summary>
    public static IReadOnlyList<string> Add(
        IEnumerable<string>? existing, string? code, int max = DefaultMax)
    {
        var current = existing?.ToList() ?? [];

        if (string.IsNullOrWhiteSpace(code)
            || LanguageCatalog.IsAutoDetect(code)
            || LanguageCatalog.IsNoTranslation(code))
        {
            return Dedupe(current, max);
        }

        var result = new List<string> { code };
        result.AddRange(current.Where(c => !string.Equals(c, code, StringComparison.OrdinalIgnoreCase)));
        return Dedupe(result, max);
    }

    private static IReadOnlyList<string> Dedupe(IEnumerable<string> codes, int max)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<string>();
        foreach (var c in codes)
        {
            if (string.IsNullOrWhiteSpace(c) || !seen.Add(c))
                continue;
            result.Add(c);
            if (result.Count >= max)
                break;
        }
        return result;
    }
}
