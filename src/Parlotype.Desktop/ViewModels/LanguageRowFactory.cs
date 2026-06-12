using System.Windows.Input;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// A pinned special row offered above the searchable list (e.g. "System keyboard
/// layout", "Auto-detect", "Off — no translation").
/// </summary>
public sealed record LanguageSpecialRow(
    string Code, string Label, string? SubHint, LanguageRowIcon Icon);

/// <summary>
/// Builds the rows shown in a language picker popover. Pure presentation logic —
/// the caller decides which catalog to display, which codes are "recent", which
/// specials to pin, and which command each row invokes.
///
/// <para>At rest (no query): specials first, then — when the list is long enough
/// to warrant search — a "Recent" group (role MRU ∩ supported) and an
/// "All languages" group; short lists get flat rows with no headers. While a
/// query is active, specials and the Recent cluster are hidden (the user is
/// hunting a language at that point) and the filtered list is flat (spec §6).</para>
/// </summary>
public static class LanguageRowFactory
{
    /// <summary>
    /// Lists longer than this get a search box and Recent/All group labels;
    /// shorter lists show neither (spec §6: "> 8 entries").
    /// </summary>
    public const int SearchThreshold = 8;

    /// <summary>
    /// Builds an ordered, de-duplicated list of <see cref="LanguageDisplayItem"/>
    /// rows for the given catalog + recents + specials + filter text. Selection
    /// state (<see cref="LanguageDisplayItem.IsSelected"/>) is left to the caller.
    /// </summary>
    public static IEnumerable<LanguageDisplayItem> Build(
        IReadOnlyList<LanguageInfo> supported,
        IReadOnlyList<string> recents,
        IReadOnlyList<LanguageSpecialRow> specials,
        string filter,
        ICommand selectCommand)
    {
        var searching = !string.IsNullOrWhiteSpace(filter);

        if (searching)
        {
            // Flat filtered list — no specials, no Recent cluster, no headers.
            foreach (var info in supported)
            {
                if (Matches(info, filter))
                    yield return LanguageDisplayItem.Language(info, isRecent: false, selectCommand);
            }
            yield break;
        }

        foreach (var special in specials)
        {
            yield return LanguageDisplayItem.Special(
                special.Code, special.Label, special.SubHint, special.Icon, selectCommand);
        }

        var grouped = supported.Count > SearchThreshold;
        var byCode = supported.ToDictionary(l => l.Code, StringComparer.OrdinalIgnoreCase);
        var pinned = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var recentRows = new List<LanguageDisplayItem>();
        foreach (var code in recents)
        {
            if (byCode.TryGetValue(code, out var info) && pinned.Add(code))
                recentRows.Add(LanguageDisplayItem.Language(info, isRecent: true, selectCommand));
        }

        if (recentRows.Count > 0)
        {
            if (grouped)
                yield return LanguageDisplayItem.Header("Recent");
            foreach (var row in recentRows)
                yield return row;
        }

        if (grouped)
            yield return LanguageDisplayItem.Header("All languages");

        foreach (var info in supported)
        {
            if (!pinned.Contains(info.Code))
                yield return LanguageDisplayItem.Language(info, isRecent: false, selectCommand);
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
