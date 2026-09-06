namespace Parlotype.Core.Localization;

/// <summary>
/// One language the Parlotype interface is available in (ADR-064).
/// </summary>
/// <param name="CultureName">
/// The .NET culture name the resource lookup runs under — neutral where possible
/// (<c>ru</c>, not <c>ru-RU</c>), because the copy carries no region-specific
/// vocabulary and a neutral satellite serves every region of that language.
/// </param>
/// <param name="EndonymName">
/// The language's name in itself — "Русский", not "Russian". Deliberately never
/// translated: someone who has landed in a language they cannot read needs to
/// find their own in the list, and the endonym is the only spelling that works
/// from every other locale.
/// </param>
public sealed record UiLanguage(string CultureName, string EndonymName)
{
    /// <summary>
    /// True for the one language whose copy lives in the neutral resource file
    /// rather than a satellite one — English. There is no <c>Strings.en.resx</c>
    /// and there should never be one; <c>Strings.resx</c> is it.
    /// </summary>
    public bool IsNeutral => CultureName == SupportedUiLanguages.NeutralCultureName;
}
