using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Reusable picker popover content (search + grouped list) shared by the source
/// and target language surfaces. The parent owns persistence and the MRU lists;
/// this VM only renders rows and routes selections back via the <c>onSelect</c>
/// callback supplied at construction.
///
/// <para>The catalog, recents, specials, and current selection are passed in as
/// callbacks (not snapshots) so the parent can mutate them between
/// <see cref="Refresh"/> calls without re-creating the picker.</para>
/// </summary>
public sealed partial class LanguagePickerViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<LanguageInfo>> _getSupported;
    private readonly Func<IReadOnlyList<string>> _getRecents;
    private readonly Func<string?> _getSelectedCode;
    private readonly Action<string> _onSelect;
    private readonly Func<IReadOnlyList<LanguageSpecialRow>> _getSpecials;

    public string Header { get; }

    public ObservableCollection<LanguageDisplayItem> Items { get; } = [];

    /// <summary>Live search text. Setting this triggers a list rebuild.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NoResultsText))]
    private string _filter = "";

    /// <summary>True when the current filter excludes every catalog row.</summary>
    [ObservableProperty]
    private bool _hasNoResults;

    /// <summary>
    /// True when the supported list is long enough to warrant the search box
    /// (and Recent/All grouping) — spec §6's "> 8 entries" rule.
    /// </summary>
    [ObservableProperty]
    private bool _showSearch;

    /// <summary>Empty-state line naming the query (spec §6).</summary>
    public string NoResultsText => $"No languages match \"{Filter.Trim()}\".";

    /// <summary>
    /// Drives the popover's open state. Owned by the parent section, set when it
    /// opens/closes this picker; light-dismiss writes false back through the
    /// popup binding.
    /// </summary>
    [ObservableProperty]
    private bool _isOpen;

    partial void OnFilterChanged(string value) => Refresh();

    public LanguagePickerViewModel(
        string header,
        Func<IReadOnlyList<LanguageInfo>> getSupported,
        Func<IReadOnlyList<string>> getRecents,
        Func<string?> getSelectedCode,
        Action<string> onSelect,
        Func<IReadOnlyList<LanguageSpecialRow>>? getSpecials = null)
    {
        Header = header;
        _getSupported = getSupported;
        _getRecents = getRecents;
        _getSelectedCode = getSelectedCode;
        _onSelect = onSelect;
        _getSpecials = getSpecials ?? (() => []);
    }

    /// <summary>
    /// Rebuilds <see cref="Items"/> from the latest catalog, recents, specials,
    /// filter, and selected code. Call this when any of those inputs change
    /// outside the picker (e.g. the parent switches engines, or a selection
    /// elsewhere mutates the MRU). The filter setter calls this automatically.
    /// </summary>
    public void Refresh()
    {
        var supported = _getSupported();
        var selectedCode = _getSelectedCode() ?? "";

        ShowSearch = supported.Count > LanguageRowFactory.SearchThreshold;

        var rows = LanguageRowFactory.Build(
            supported,
            recents: _getRecents(),
            specials: _getSpecials(),
            filter: Filter,
            selectCommand: SelectCommand);

        Items.Clear();
        var hasSelectable = false;
        foreach (var row in rows)
        {
            row.IsSelected = !row.IsHeader
                && string.Equals(row.Code, selectedCode, StringComparison.OrdinalIgnoreCase);
            hasSelectable |= !row.IsHeader;
            Items.Add(row);
        }
        HasNoResults = !hasSelectable;
    }

    [RelayCommand]
    private void Select(string code) => _onSelect(code);
}
