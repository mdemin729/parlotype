using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Interactivity;
using Avalonia.VisualTree;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
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

    [AvaloniaTheory]
    [InlineData("ConfirmButton", true)]
    [InlineData("CancelButton", false)]
    [InlineData(null, null)]
    public async Task StandaloneDialog_IsVisibleAndReturnsChoice(string? buttonName, bool? expected)
    {
        var dialog = new ConfirmationDialog
        {
            DataContext = new ConfirmationDialogViewModel("Title", "Message", "OK", "Cancel")
        };

        try
        {
            var result = dialog.ShowStandaloneAsync();
            Assert.True(dialog.IsVisible);
            Assert.False(result.IsCompleted);
            Assert.Null(dialog.Owner);

            if (buttonName is null)
                dialog.Close();
            else
                Click(dialog, buttonName);

            Assert.Equal(expected, await result);
            Assert.False(dialog.IsVisible);
        }
        finally
        {
            dialog.Close();
        }
    }

    [AvaloniaFact]
    public async Task RecordingFailure_RealDialogService_ShowsAndDismissesMessageWithoutOwner()
    {
        ConfirmationDialog? shown = null;
        using var subscription = Window.WindowOpenedEvent.AddClassHandler<ConfirmationDialog>(
            (dialog, _) => shown = dialog);
        var pipeline = new MockAudioPipeline { ThrowOnStart = new ArgumentException("Source must be stereo") };
        var vm = new TranscribeViewModel(
            new MockWindowManager(), pipeline, dialogService: new UserDialogService());

        try
        {
            await vm.TogglePlayCommand.ExecuteAsync(null);

            Assert.NotNull(shown);
            Assert.True(shown.IsVisible);
            Assert.Null(shown.Owner);
            Assert.Equal(Strings.Dialog_RecordingStartFailed_Title, shown.Title);
            Assert.Contains(shown.GetVisualDescendants().OfType<TextBlock>(),
                text => text.IsVisible && text.Text == Strings.Dialog_RecordingStartFailed_Message);
            Assert.False(shown.FindControl<Button>("CancelButton")!.IsVisible);

            Click(shown, "ConfirmButton");
            Assert.False(shown.IsVisible);
            Assert.False(vm.IsRecording);
            Assert.Equal(Strings.Transcribe_Status_StartFailed, vm.StatusText);
        }
        finally
        {
            shown?.Close();
        }
    }
}
