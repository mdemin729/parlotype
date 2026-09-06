using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using Parlotype.Core.Settings;

namespace Parlotype.Desktop.ViewModels;

public sealed partial class ThemeDisplayItem(AppTheme theme, string displayName, ICommand selectCommand)
    : ObservableObject
{
    public AppTheme Theme { get; } = theme;

    // Observable since ADR-064: the label is resx copy built in the view model,
    // so switching the interface language has to rewrite it in place.
    [ObservableProperty]
    private string _displayName = displayName;

    public ICommand SelectCommand { get; } = selectCommand;
}
