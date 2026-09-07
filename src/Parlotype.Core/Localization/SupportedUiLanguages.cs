namespace Parlotype.Core.Localization;

/// <summary>
/// The registry of interface languages Parlotype ships (ADR-064) — the single
/// place a new language is declared.
/// </summary>
/// <remarks>
/// <para>
/// Adding a language is one row here plus one <c>Strings.&lt;culture&gt;.resx</c>
/// file. Everything that needs to know the set reads it from here: the settings
/// picker builds its rows from <see cref="All"/>, and the localization parity
/// check walks <see cref="Translated"/> to decide which resx files must exist and
/// must be complete. Nothing else may hardcode a language list, or the two will
/// drift and a locale will go stale without anything noticing.
/// </para>
/// <para>
/// Lives in Core rather than Desktop because the parity test and tooling need it
/// without taking a dependency on the UI assembly. Core holds the identity of a
/// language; the display copy around it (the "System default" row label) belongs
/// to Desktop, where the resources are.
/// </para>
/// </remarks>
public static class SupportedUiLanguages
{
    /// <summary>
    /// Stored value of <c>SettingsKeys.UiLanguage</c> meaning "follow the
    /// operating system". The default when the key is unset, and not a culture
    /// name — it resolves to one at startup.
    /// </summary>
    public const string SystemSettingValue = "system";

    /// <summary>
    /// The culture of <c>Strings.resx</c> itself. Resource fallback ends here, so
    /// this is what an unsupported OS language, a missing satellite assembly and
    /// an untranslated key all land on.
    /// </summary>
    public const string NeutralCultureName = "en";

    /// <summary>Every interface language, in the order the settings picker shows them.</summary>
    public static IReadOnlyList<UiLanguage> All { get; } =
    [
        new("en", "English"),
        new("ru", "Русский"),
        new("es", "Español"),
    ];

    /// <summary>
    /// The languages that need a satellite <c>Strings.&lt;culture&gt;.resx</c> —
    /// everything except the neutral one. What the parity check iterates.
    /// </summary>
    public static IEnumerable<UiLanguage> Translated => All.Where(l => !l.IsNeutral);

    /// <summary>
    /// The language with this culture name, or <see langword="null"/> if Parlotype
    /// does not ship it.
    /// </summary>
    public static UiLanguage? Find(string? cultureName) =>
        cultureName is null
            ? null
            : All.FirstOrDefault(l =>
                string.Equals(l.CultureName, cultureName, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Resolves a stored setting value to the culture name to run the UI under,
    /// or <see langword="null"/> to follow the operating system.
    /// </summary>
    /// <remarks>
    /// Unset, <see cref="SystemSettingValue"/>, and any value naming a language we
    /// no longer ship all answer "follow the system". That last case is the one
    /// that matters: a language removed in a later release must degrade to the
    /// system default rather than pinning the user to a culture with no resources.
    /// </remarks>
    public static string? ResolveSettingValue(string? stored) =>
        string.IsNullOrWhiteSpace(stored) || stored == SystemSettingValue
            ? null
            : Find(stored)?.CultureName;

    /// <summary>
    /// The shipping language that best serves <paramref name="systemCultureName"/>,
    /// or <see langword="null"/> when none does and English should be used.
    /// </summary>
    /// <remarks>
    /// Matches on the neutral part, so <c>ru-RU</c>, <c>ru-BY</c> and <c>ru</c> all
    /// find Russian. .NET's own satellite fallback would do this at lookup time;
    /// doing it explicitly here is what lets the settings picker show which
    /// concrete language "System default" currently resolves to.
    /// </remarks>
    public static UiLanguage? MatchSystemCulture(string? systemCultureName)
    {
        if (string.IsNullOrWhiteSpace(systemCultureName))
            return null;

        var separator = systemCultureName.IndexOf('-');
        var neutral = separator < 0 ? systemCultureName : systemCultureName[..separator];

        return Find(systemCultureName) ?? Find(neutral);
    }
}
