using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using Parlotype.WinUI.ViewModels;

namespace Parlotype.WinUI.Views;

public sealed partial class AudioSettingsPage : Page
{
    public AudioSettingsViewModel ViewModel { get; }

    public AudioSettingsPage()
    {
        ViewModel = App.Services.GetRequiredService<AudioSettingsViewModel>();
        InitializeComponent();
    }
}
