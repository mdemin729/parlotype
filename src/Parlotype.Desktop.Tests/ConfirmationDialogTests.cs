using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class ConfirmationDialogTests
{
    private static (ConfirmationDialog Dialog, Window Owner, Task<bool?> Result) ShowDialog()
    {
        var owner = new Window();
        owner.Show();

        var dialog = new ConfirmationDialog
        {
            DataContext = new ConfirmationDialogViewModel(
                "Cloud provider not configured",
                "No API key configured.",
                "Open settings",
                "Cancel"),
        };

        var result = dialog.ShowDialog<bool?>(owner);
        return (dialog, owner, result);
    }

    private static void Click(ConfirmationDialog dialog, string buttonName)
    {
        var button = dialog.FindControl<Button>(buttonName);
        Assert.NotNull(button);
        button!.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
    }

    [AvaloniaFact]
    public async Task ConfirmButton_ClosesWithTrue()
    {
        var (dialog, owner, result) = ShowDialog();

        Click(dialog, "ConfirmButton");

        Assert.True(await result);
        owner.Close();
    }

    [AvaloniaFact]
    public async Task CancelButton_ClosesWithFalse()
    {
        var (dialog, owner, result) = ShowDialog();

        Click(dialog, "CancelButton");

        Assert.False(await result);
        owner.Close();
    }

    [AvaloniaFact]
    public async Task WindowClose_YieldsNull_TreatedAsCancel()
    {
        var (dialog, owner, result) = ShowDialog();

        dialog.Close();

        Assert.Null(await result);
        owner.Close();
    }
}
