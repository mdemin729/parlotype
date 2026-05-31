using System.Globalization;

namespace Parlotype.Core.Speech;

/// <summary>
/// Source of truth for selectable languages.
///
/// <para>
/// Two catalogs are exposed: <see cref="WhisperLanguages"/> — the fixed set of
/// languages the Whisper models recognise (the library does not expose this, so
/// it is hand-curated here) — and <see cref="AllLanguages"/>, derived at runtime
/// from <see cref="CultureInfo.GetCultures(CultureTypes)"/>, used as the fallback
/// "full list" for engines (e.g. an LLM) that don't declare a fixed set.
/// </para>
///
/// <para>
/// Native names are taken from <see cref="CultureInfo"/> when the code resolves to
/// a known culture; otherwise the English name is reused.
/// </para>
/// </summary>
public static class LanguageCatalog
{
    /// <summary>Sentinel code meaning "let the model detect the source language".</summary>
    public const string AutoDetectCode = "auto";

    /// <summary>Sentinel code meaning "do not translate" (output the source language).</summary>
    public const string NoTranslationCode = "none";

    // Curated Whisper language set (ISO 639-1 where available, plus Whisper-specific
    // codes such as "yue", "jw", "haw"). English names match the Whisper tokenizer.
    private static readonly (string Code, string Name)[] WhisperRaw =
    [
        ("en", "English"), ("zh", "Chinese"), ("de", "German"), ("es", "Spanish"),
        ("ru", "Russian"), ("ko", "Korean"), ("fr", "French"), ("ja", "Japanese"),
        ("pt", "Portuguese"), ("tr", "Turkish"), ("pl", "Polish"), ("ca", "Catalan"),
        ("nl", "Dutch"), ("ar", "Arabic"), ("sv", "Swedish"), ("it", "Italian"),
        ("id", "Indonesian"), ("hi", "Hindi"), ("fi", "Finnish"), ("vi", "Vietnamese"),
        ("he", "Hebrew"), ("uk", "Ukrainian"), ("el", "Greek"), ("ms", "Malay"),
        ("cs", "Czech"), ("ro", "Romanian"), ("da", "Danish"), ("hu", "Hungarian"),
        ("ta", "Tamil"), ("no", "Norwegian"), ("th", "Thai"), ("ur", "Urdu"),
        ("hr", "Croatian"), ("bg", "Bulgarian"), ("lt", "Lithuanian"), ("la", "Latin"),
        ("mi", "Maori"), ("ml", "Malayalam"), ("cy", "Welsh"), ("sk", "Slovak"),
        ("te", "Telugu"), ("fa", "Persian"), ("lv", "Latvian"), ("bn", "Bengali"),
        ("sr", "Serbian"), ("az", "Azerbaijani"), ("sl", "Slovenian"), ("kn", "Kannada"),
        ("et", "Estonian"), ("mk", "Macedonian"), ("br", "Breton"), ("eu", "Basque"),
        ("is", "Icelandic"), ("hy", "Armenian"), ("ne", "Nepali"), ("mn", "Mongolian"),
        ("bs", "Bosnian"), ("kk", "Kazakh"), ("sq", "Albanian"), ("sw", "Swahili"),
        ("gl", "Galician"), ("mr", "Marathi"), ("pa", "Punjabi"), ("si", "Sinhala"),
        ("km", "Khmer"), ("sn", "Shona"), ("yo", "Yoruba"), ("so", "Somali"),
        ("af", "Afrikaans"), ("oc", "Occitan"), ("ka", "Georgian"), ("be", "Belarusian"),
        ("tg", "Tajik"), ("sd", "Sindhi"), ("gu", "Gujarati"), ("am", "Amharic"),
        ("yi", "Yiddish"), ("lo", "Lao"), ("uz", "Uzbek"), ("fo", "Faroese"),
        ("ht", "Haitian Creole"), ("ps", "Pashto"), ("tk", "Turkmen"), ("nn", "Nynorsk"),
        ("mt", "Maltese"), ("sa", "Sanskrit"), ("lb", "Luxembourgish"), ("my", "Myanmar"),
        ("bo", "Tibetan"), ("tl", "Tagalog"), ("mg", "Malagasy"), ("as", "Assamese"),
        ("tt", "Tatar"), ("haw", "Hawaiian"), ("ln", "Lingala"), ("ha", "Hausa"),
        ("ba", "Bashkir"), ("jw", "Javanese"), ("su", "Sundanese"), ("yue", "Cantonese"),
    ];

    private static readonly IReadOnlyList<LanguageInfo> WhisperList =
        WhisperRaw
            .Select(x => new LanguageInfo(x.Code, x.Name, ResolveNativeName(x.Code, x.Name)))
            .ToArray();

    private static readonly IReadOnlyList<LanguageInfo> AllList = BuildAllLanguages();

    private static readonly IReadOnlyDictionary<string, LanguageInfo> ByCode =
        AllList
            .Concat(WhisperList)
            .GroupBy(l => l.Code, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

    /// <summary>The fixed set of languages Whisper recognises.</summary>
    public static IReadOnlyList<LanguageInfo> WhisperLanguages => WhisperList;

    /// <summary>
    /// The full list of languages from the .NET runtime's culture data, used as a
    /// fallback for engines that don't declare a fixed language set.
    /// </summary>
    public static IReadOnlyList<LanguageInfo> AllLanguages => AllList;

    /// <summary>
    /// Returns the <see cref="LanguageInfo"/> for <paramref name="code"/>, or null
    /// for unknown codes and the <see cref="AutoDetectCode"/> /
    /// <see cref="NoTranslationCode"/> sentinels.
    /// </summary>
    public static LanguageInfo? TryGet(string? code) =>
        !string.IsNullOrWhiteSpace(code) && ByCode.TryGetValue(code, out var info) ? info : null;

    /// <summary>
    /// Returns the English display name for <paramref name="code"/>, or the code
    /// itself if it is unknown.
    /// </summary>
    public static string GetEnglishName(string? code) =>
        TryGet(code)?.EnglishName ?? code ?? string.Empty;

    /// <summary>True when <paramref name="code"/> is the auto-detect sentinel.</summary>
    public static bool IsAutoDetect(string? code) =>
        string.IsNullOrWhiteSpace(code) || string.Equals(code, AutoDetectCode, StringComparison.OrdinalIgnoreCase);

    /// <summary>True when <paramref name="code"/> represents "no translation".</summary>
    public static bool IsNoTranslation(string? code) =>
        string.IsNullOrWhiteSpace(code) || string.Equals(code, NoTranslationCode, StringComparison.OrdinalIgnoreCase);

    private static string ResolveNativeName(string code, string fallback)
    {
        try
        {
            var culture = CultureInfo.GetCultureInfo(code);
            // Neutral cultures yield a sensible NativeName; guard against the
            // invariant culture (empty name) which GetCultureInfo never throws for.
            return string.IsNullOrWhiteSpace(culture.NativeName) ? fallback : culture.NativeName;
        }
        catch (CultureNotFoundException)
        {
            return fallback;
        }
    }

    private static IReadOnlyList<LanguageInfo> BuildAllLanguages()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var result = new List<LanguageInfo>();

        foreach (var culture in CultureInfo.GetCultures(CultureTypes.NeutralCultures))
        {
            // Skip the invariant culture and anything without a two-letter code.
            var code = culture.TwoLetterISOLanguageName;
            if (string.IsNullOrWhiteSpace(code) || code == "iv" || !seen.Add(code))
                continue;

            var english = culture.EnglishName;
            var native = string.IsNullOrWhiteSpace(culture.NativeName) ? english : culture.NativeName;
            result.Add(new LanguageInfo(code, english, native));
        }

        return result.OrderBy(l => l.EnglishName, StringComparer.OrdinalIgnoreCase).ToArray();
    }
}
