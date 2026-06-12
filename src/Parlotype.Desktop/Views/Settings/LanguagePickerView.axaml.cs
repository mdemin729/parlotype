using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Views.Settings;

/// <summary>
/// Popover content for the language pickers. Hosted inside a <c>Popup</c> by the
/// page view; attaches fresh on every open, which is when the search box gets
/// focus. Escape closes the popover (light dismiss handles outside clicks).
/// </summary>
public partial class LanguagePickerView : UserControl
{
    public LanguagePickerView() => InitializeComponent();
    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // Auto-focus the search box when the popover opens (spec §6). The view
        // is re-loaded on each Popup open, so this fires per open.
        if (DataContext is LanguagePickerViewModel { ShowSearch: true }
            && this.FindControl<TextBox>("SearchBox") is { } searchBox)
        {
            searchBox.Focus();
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape && DataContext is LanguagePickerViewModel vm)
        {
            vm.IsOpen = false;
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }
}
