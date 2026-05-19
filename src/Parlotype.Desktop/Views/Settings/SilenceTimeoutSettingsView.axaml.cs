using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.Views.Settings;

public partial class SilenceTimeoutSettingsView : UserControl
{
    public SilenceTimeoutSettingsView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
