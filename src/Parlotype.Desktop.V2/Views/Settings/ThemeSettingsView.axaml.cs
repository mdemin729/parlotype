using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.V2.Views.Settings;

public partial class ThemeSettingsView : UserControl
{
    public ThemeSettingsView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
