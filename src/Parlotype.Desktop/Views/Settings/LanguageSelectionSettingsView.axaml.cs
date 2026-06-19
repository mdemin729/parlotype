using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Parlotype.Desktop.ViewModels.Settings;

namespace Parlotype.Desktop.Views.Settings;

public partial class LanguageSelectionSettingsView : UserControl
{
    public LanguageSelectionSettingsView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnAttachedToVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        (DataContext as LanguageSelectionSettingsViewModel)?.Relationship.BeginLivePolling();
    }

    protected override void OnDetachedFromVisualTree(Avalonia.VisualTreeAttachmentEventArgs e)
    {
        (DataContext as LanguageSelectionSettingsViewModel)?.Relationship.EndLivePolling();
        base.OnDetachedFromVisualTree(e);
    }
}
