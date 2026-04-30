using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.V2.Views.Settings;

public partial class MicrophoneSettingsView : UserControl
{
    public MicrophoneSettingsView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
