using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.V2.Views.Settings;

public partial class SpeechSettingsView : UserControl
{
    public SpeechSettingsView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
