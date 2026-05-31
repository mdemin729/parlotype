using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Parlotype.Desktop.ViewModels;

/// <summary>
/// A selectable language row in the source/target language pickers. Carries its
/// own select command (flyout/list items are detached from the visual tree, so
/// commands are embedded rather than traversed via <c>$parent</c>).
/// </summary>
public sealed partial class LanguageDisplayItem(
    string code, string displayName, bool isRecent, ICommand selectCommand)
    : ObservableObject
{
    /// <summary>Language code, or a sentinel ("auto" / "none").</summary>
    public string Code { get; } = code;

    /// <summary>Display label (e.g. "Russian — Русский", "Auto-detect").</summary>
    public string DisplayName { get; } = displayName;

    /// <summary>True when this row is a recently-used language pinned to the top.</summary>
    public bool IsRecent { get; } = isRecent;

    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    private bool _isSelected;
}
