using System.Windows.Input;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Builds the rows shown in a language picker. Pure presentation logic — the
/// caller decides which catalog to display, which codes are "recent", and which
/// command each row invokes. Used by <see cref="LanguagePickerViewModel"/> and
/// (for now) by <c>LanguageSelectionSettingsViewModel</c>.
///
/// <para>Rows are ordered as: optional leading sentinel (e.g. "Auto-detect" /
/// "Default — no translation"), recent languages pinned to the top, then the
/// remaining supported languages in catalog order. Each language appears once.
/// The leading sentinel is hidden while a non-empty filter is active — it is a
/// mode, not a language, and the user expects the search to scope to actual
/// languages.</para>
/// </summary>
public static class LanguageRowFactory
{
    /// <summary>
    /// Builds an ordered, de-duplicated list of <see cref="LanguageDisplayItem"/>
    /// rows for the given catalog + recent codes + filter text. The selection
    /// state (<see cref="LanguageDisplayItem.IsSelected"/>) is left to the caller.
    /// </summary>
    public static IEnumerable<LanguageDisplayItem> Build(
        IReadOnlyList<LanguageInfo> supported,
        IReadOnlyList<string> recents,
        (string Code, string Label)? leadingSentinel,
        string filter,
        ICommand selectCommand)
    {
        if (leadingSentinel is { } sentinel && string.IsNullOrWhiteSpace(filter))
        {
            yield return new LanguageDisplayItem(
                sentinel.Code, sentinel.Label, isRecent: false, selectCommand);
        }

        var byCode = supported.ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase);
        var pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var code in recents)
        {
            if (byCode.TryGetValue(code, out var info)
                && Matches(info, filter)
                && pinned.Add(code))
            {
                yield return new LanguageDisplayItem(
                    info.Code, LanguageCatalog.GetDisplayLabel(info.Code), isRecent: true, selectCommand);
            }
        }

        foreach (var info in supported)
        {
            if (Matches(info, filter) && !pinned.Contains(info.Code))
            {
                yield return new LanguageDisplayItem(
                    info.Code, LanguageCatalog.GetDisplayLabel(info.Code), isRecent: false, selectCommand);
            }
        }
    }

    /// <summary>
    /// True when <paramref name="info"/> matches <paramref name="filter"/>.
    /// Blank filter matches everything. Match is case-insensitive against
    /// English name, native name, or ISO code.
    /// </summary>
    public static bool Matches(LanguageInfo info, string filter)
    {
        if (string.IsNullOrWhiteSpace(filter))
            return true;

        var f = filter.Trim();
        return info.EnglishName.Contains(f, StringComparison.OrdinalIgnoreCase)
            || info.NativeName.Contains(f, StringComparison.OrdinalIgnoreCase)
            || info.Code.Contains(f, StringComparison.OrdinalIgnoreCase);
    }
}
