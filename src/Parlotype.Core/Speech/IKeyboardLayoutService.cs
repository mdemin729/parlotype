namespace Parlotype.Core.Speech;

/// <summary>
/// The OS keyboard layout resolved to a language.
/// </summary>
/// <param name="LanguageCode">
/// ISO 639-1 two-letter language code (e.g. <c>"en"</c>, <c>"ru"</c>), suitable
/// for lookup in <see cref="LanguageCatalog"/>.
/// </param>
/// <param name="FriendlyName">
/// Human-readable layout name including the region (e.g. "English (United States)"),
/// shown as the sub-hint on the "System keyboard layout" source option.
/// </param>
public sealed record KeyboardLayoutInfo(string LanguageCode, string FriendlyName);

/// <summary>
/// Detects the language of the active OS keyboard layout, backing the
/// <see cref="LanguageCatalog.KeyboardLayoutCode"/> source sentinel.
/// </summary>
public interface IKeyboardLayoutService
{
    /// <summary>
    /// Returns the active keyboard layout's language, or <c>null</c> when
    /// detection is unavailable on this platform. Never throws.
    /// </summary>
    KeyboardLayoutInfo? Detect();
}
