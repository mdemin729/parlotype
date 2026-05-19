using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.Views.Settings;

public partial class WhisperOutputSettingsView : UserControl
{
    public WhisperOutputSettingsView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
