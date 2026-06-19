using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Views;

public partial class TranscribeWindow : Window
{
    public TranscribeWindow()
    {
        InitializeComponent();
        Opened += (_, _) => (DataContext as TranscribeViewModel)?.Relationship?.BeginLivePolling();
        Closed += (_, _) => (DataContext as TranscribeViewModel)?.Relationship?.EndLivePolling();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);
}
