using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// Reusable picker (search + list) shared by the source and target language
/// surfaces on the Language settings page. The parent section owns persistence
/// and the MRU lists; this VM only renders rows and routes selections back via
/// the <c>onSelect</c> callback supplied at construction.
///
/// <para>The catalog, recents, and current selection are passed in as callbacks
/// (not snapshots) so the parent can mutate them between <see cref="Refresh"/>
/// calls without re-creating the picker.</para>
/// </summary>
public sealed partial class LanguagePickerViewModel : ObservableObject
{
    private readonly Func<IReadOnlyList<LanguageInfo>> _getSupported;
    private readonly Func<IReadOnlyList<string>> _getRecents;
    private readonly Func<string?> _getSelectedCode;
    private readonly Action<string> _onSelect;
    private readonly Func<(string Code, string Label)?> _getLeadingSentinel;

    public string Header { get; }

    public ObservableCollection<LanguageDisplayItem> Items { get; } = [];

    /// <summary>Live search text. Setting this triggers a list rebuild.</summary>
    [ObservableProperty]
    private string _filter = "";

    /// <summary>True when the current filter excludes every catalog row.</summary>
    [ObservableProperty]
    private bool _hasNoResults;

    /// <summary>
    /// Drives the picker view's visibility. Owned by the parent section, set
    /// when it opens/closes this picker. Lives on the picker VM so the View
    /// binds to its own DataContext without traversing to the host.
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
        Func<(string Code, string Label)?>? getLeadingSentinel = null)
    {
        Header = header;
        _getSupported = getSupported;
        _getRecents = getRecents;
        _getSelectedCode = getSelectedCode;
        _onSelect = onSelect;
        _getLeadingSentinel = getLeadingSentinel ?? (() => null);
    }

    /// <summary>
    /// Rebuilds <see cref="Items"/> from the latest catalog, recents, filter, and
    /// selected code. Call this when any of those inputs change outside the
    /// picker (e.g. the parent switches engines, or a selection elsewhere mutates
    /// the MRU). The filter setter calls this automatically.
    /// </summary>
    public void Refresh()
    {
        var selectedCode = _getSelectedCode() ?? "";

        var rows = LanguageRowFactory.Build(
            supported: _getSupported(),
            recents: _getRecents(),
            leadingSentinel: _getLeadingSentinel(),
            filter: Filter,
            selectCommand: SelectCommand);

        Items.Clear();
        foreach (var row in rows)
        {
            row.IsSelected = string.Equals(row.Code, selectedCode, StringComparison.OrdinalIgnoreCase);
            Items.Add(row);
        }
        HasNoResults = Items.Count == 0;
    }

    [RelayCommand]
    private void Select(string code) => _onSelect(code);
}
