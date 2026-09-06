using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Resources;

namespace Parlotype.Desktop.ViewModels;

public sealed partial class WaitTimeDisplayItem(WaitTimeOption option, ICommand selectCommand)
    : ObservableObject
{
    public WaitTimeOption Option { get; } = option;

    [ObservableProperty]
    private string _displayName = Describe(option);

    public ICommand SelectCommand { get; } = selectCommand;

    [ObservableProperty]
    private bool _isSelected;

    /// <summary>Rebuilds the label after an interface-language change (ADR-064).</summary>
    internal void RefreshDisplayName() => DisplayName = Describe(Option);

    /// <summary>
    /// The option's name and its duration — "Long (1s)". The name is resx copy;
    /// the number is formatted under <c>CurrentCulture</c>, so it follows the
    /// user's Windows regional settings (a decimal comma where that is the local
    /// convention) independently of the interface language.
    /// </summary>
    /// <remarks>
    /// The English names used to live on <c>WaitTimeOptionExtensions</c> in Core.
    /// They moved here in ADR-064: Core has no resources, and "Medium"/"Very Long"
    /// are prose shown to the user rather than anything the pipeline runs on.
    /// </remarks>
    private static string Describe(WaitTimeOption option) =>
        Strings.Format_Settings_SilenceTimeout_WaitFormat(NameOf(option), option.GetSeconds());

    private static string NameOf(WaitTimeOption option) => option switch
    {
        WaitTimeOption.Medium => Strings.Settings_SilenceTimeout_Wait_Medium,
        WaitTimeOption.Long => Strings.Settings_SilenceTimeout_Wait_Long,
        WaitTimeOption.Extended => Strings.Settings_SilenceTimeout_Wait_Extended,
        WaitTimeOption.VeryLong => Strings.Settings_SilenceTimeout_Wait_VeryLong,
        _ => option.ToString(),
    };
}
