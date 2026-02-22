using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Views;

public partial class SettingsFlyoutView : UserControl
{
    public SettingsFlyoutView()
    {
        InitializeComponent();

        var modelButton = this.FindControl<Button>("ModelPickerButton");
        if (modelButton?.Flyout is PopupFlyoutBase flyout)
            flyout.Opening += OnModelPickerOpening;
    }

    private void OnModelPickerOpening(object? sender, EventArgs e)
    {
        if (DataContext is SettingsViewModel vm)
            vm.RefreshModelCacheStatus();
    }
}
