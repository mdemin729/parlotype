using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Parlotype.WinUI.ViewModels;

namespace Parlotype.WinUI.Views;

public sealed partial class AppearancePage : Page
{
    private bool _suppressSelectionChanged;

    public AppearanceViewModel ViewModel { get; }

    public AppearancePage()
    {
        ViewModel = App.Services.GetRequiredService<AppearanceViewModel>();
        InitializeComponent();
        ThemeList.ItemsSource = AppearanceViewModel.ThemeItems;
        Loaded += AppearancePage_Loaded;
    }

    private async void AppearancePage_Loaded(object sender, RoutedEventArgs e)
    {
        await ViewModel.InitializeAsync();

        // Sync ListView selection with the loaded theme without triggering a redundant save.
        _suppressSelectionChanged = true;
        var index = Array.IndexOf(
            AppearanceViewModel.ThemeItems,
            Array.Find(AppearanceViewModel.ThemeItems, t => t.Theme == ViewModel.SelectedTheme));
        if (index >= 0)
            ThemeList.SelectedIndex = index;
        _suppressSelectionChanged = false;
    }

    private void ThemeList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressSelectionChanged)
            return;

        if (ThemeList.SelectedItem is ThemeItem item)
        {
            ViewModel.SelectThemeCommand.Execute(item.Theme);
        }
    }
}
