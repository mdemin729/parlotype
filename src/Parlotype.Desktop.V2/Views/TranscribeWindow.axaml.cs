using Avalonia.Controls;
using Avalonia.Markup.Xaml;

namespace Parlotype.Desktop.V2.Views;

public partial class TranscribeWindow : Window
{
    public TranscribeWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
