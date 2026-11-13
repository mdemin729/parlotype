using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels;

/// <summary>One writing style on the Speech cleanup page (ADR-073).</summary>
public sealed partial class CleanupStylingDisplayItem(CleanupStyling styling, ICommand selectCommand)
    : ObservableObject
{
    public CleanupStyling Styling { get; } = styling;

    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    private string _displayName = NameOf(styling);

    [ObservableProperty]
    private string _description = DescriptionOf(styling);

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Rebuilds both labels after an interface-language change (ADR-064).</summary>
    internal void RefreshText()
    {
        DisplayName = NameOf(Styling);
        Description = DescriptionOf(Styling);
    }

    private static string NameOf(CleanupStyling styling) => styling switch
    {
        CleanupStyling.Casual => Strings.Settings_SpeechCleanup_Style_Casual,
        CleanupStyling.SemiCasual => Strings.Settings_SpeechCleanup_Style_SemiCasual,
        CleanupStyling.SemiFormal => Strings.Settings_SpeechCleanup_Style_SemiFormal,
        CleanupStyling.Formal => Strings.Settings_SpeechCleanup_Style_Formal,
        _ => styling.ToString(),
    };

    private static string DescriptionOf(CleanupStyling styling) => styling switch
    {
        CleanupStyling.Casual => Strings.Settings_SpeechCleanup_Style_Casual_Description,
        CleanupStyling.SemiCasual => Strings.Settings_SpeechCleanup_Style_SemiCasual_Description,
        CleanupStyling.SemiFormal => Strings.Settings_SpeechCleanup_Style_SemiFormal_Description,
        CleanupStyling.Formal => Strings.Settings_SpeechCleanup_Style_Formal_Description,
        _ => string.Empty,
    };
}
