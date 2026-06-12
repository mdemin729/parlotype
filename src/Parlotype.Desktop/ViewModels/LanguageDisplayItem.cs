using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels;

/// <summary>Leading icon shown in a language picker row's tile.</summary>
public enum LanguageRowIcon
{
    /// <summary>Explicit language — the tile shows the language code.</summary>
    Globe,

    /// <summary>System keyboard layout special.</summary>
    Keyboard,

    /// <summary>Auto-detect special.</summary>
    Sparkle,

    /// <summary>"Off — no translation" special.</summary>
    Off,
}

/// <summary>
/// A row in a language picker popover: a selectable language, a pinned special
/// (keyboard / auto / off), or a non-interactive group header ("Recent" /
/// "All languages"). Carries its own select command (popover/list items are
/// detached from the visual tree, so commands are embedded rather than
/// traversed via <c>$parent</c>).
/// </summary>
public sealed partial class LanguageDisplayItem : ObservableObject
{
    private LanguageDisplayItem(
        string code,
        string displayName,
        string? secondaryText,
        bool isRecent,
        bool isSpecial,
        bool isHeader,
        LanguageRowIcon icon,
        ICommand? selectCommand)
    {
        Code = code;
        DisplayName = displayName;
        SecondaryText = secondaryText;
        IsRecent = isRecent;
        IsSpecial = isSpecial;
        IsHeader = isHeader;
        Icon = icon;
        SelectCommand = selectCommand;
    }

    /// <summary>Language code or sentinel ("keyboard" / "auto" / "none"); empty for headers.</summary>
    public string Code { get; }

    /// <summary>Primary label: English name, special label, or header text.</summary>
    public string DisplayName { get; }

    /// <summary>Native name (when it differs) or the special's sub-hint.</summary>
    public string? SecondaryText { get; }

    public bool HasSecondaryText => !string.IsNullOrEmpty(SecondaryText);

    /// <summary>True when this row came from the role's MRU cluster.</summary>
    public bool IsRecent { get; }

    /// <summary>True for the pinned specials (keyboard / auto / off).</summary>
    public bool IsSpecial { get; }

    /// <summary>True for non-interactive group labels.</summary>
    public bool IsHeader { get; }

    public LanguageRowIcon Icon { get; }

    /// <summary>
    /// Text rendered inside the leading icon tile: a glyph for specials, the
    /// upper-cased language code for plain language rows.
    /// </summary>
    public string TileText => Icon switch
    {
        LanguageRowIcon.Keyboard => "⌨",
        LanguageRowIcon.Sparkle => "✦",
        LanguageRowIcon.Off => "⊘",
        _ => Code.ToUpperInvariant(),
    };

    public ICommand? SelectCommand { get; }

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>A non-interactive group label row.</summary>
    public static LanguageDisplayItem Header(string label) =>
        new(code: "", label, secondaryText: null,
            isRecent: false, isSpecial: false, isHeader: true,
            LanguageRowIcon.Globe, selectCommand: null);

    /// <summary>A pinned special row (keyboard / auto / off).</summary>
    public static LanguageDisplayItem Special(
        string code, string label, string? subHint, LanguageRowIcon icon, ICommand selectCommand) =>
        new(code, label, subHint,
            isRecent: false, isSpecial: true, isHeader: false, icon, selectCommand);

    /// <summary>A selectable language row; native name shown when it differs.</summary>
    public static LanguageDisplayItem Language(LanguageInfo info, bool isRecent, ICommand selectCommand)
    {
        var secondary = string.Equals(info.EnglishName, info.NativeName, StringComparison.OrdinalIgnoreCase)
            ? null
            : info.NativeName;
        return new LanguageDisplayItem(
            info.Code, info.EnglishName, secondary,
            isRecent, isSpecial: false, isHeader: false,
            LanguageRowIcon.Globe, selectCommand);
    }
}
