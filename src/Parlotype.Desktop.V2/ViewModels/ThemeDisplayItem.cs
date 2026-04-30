using System.Windows.Input;
using Parlotype.Core.Settings;

namespace Parlotype.Desktop.V2.ViewModels;

public sealed class ThemeDisplayItem(AppTheme theme, string displayName, ICommand selectCommand)
{
    public AppTheme Theme { get; } = theme;
    public string DisplayName { get; } = displayName;
    public ICommand SelectCommand { get; } = selectCommand;
}
