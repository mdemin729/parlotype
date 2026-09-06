using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.Views.Settings;

public partial class InterfaceLanguageSettingsView : UserControl
{
    public InterfaceLanguageSettingsView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
