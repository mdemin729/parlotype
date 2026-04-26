using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Parlotype.WinUI.Views;

public sealed partial class SettingsWindow : Window
{
    public SettingsWindow()
    {
        InitializeComponent();
        Title = "Parlotype Settings";
        SystemBackdrop = new MicaBackdrop();

        // Resize to 800×600 device-independent pixels
        AppWindow.Resize(new Windows.Graphics.SizeInt32(800, 600));

        // Select the first navigation item on load
        NavView.SelectedItem = NavView.MenuItems[0];
    }

    private void NavView_SelectionChanged(
        NavigationView sender,
        NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItem is NavigationViewItem item)
        {
            var pageType = item.Tag?.ToString() switch
            {
                "audio"       => typeof(AudioSettingsPage),
                "speechmodel" => typeof(SpeechModelPage),
                "hotkeys"     => typeof(HotkeySettingsPage),
                "appearance"  => typeof(AppearancePage),
                _             => null
            };

            if (pageType is not null)
            {
                ContentFrame.Navigate(pageType);
            }
        }
    }
}
