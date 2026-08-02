using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.Views.Settings;

public partial class DataSettingsView : UserControl
{
    public DataSettingsView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
