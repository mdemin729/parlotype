using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Parlotype.Desktop.Views;

/// <summary>
/// Small confirm/cancel dialog, modal with a visible owner or standalone in
/// tray-only mode. Confirm returns true, Cancel false, and window close null.
/// Content comes from <see cref="ViewModels.ConfirmationDialogViewModel"/>.
/// </summary>
public partial class ConfirmationDialog : Window
{
    private bool? _result;

    public ConfirmationDialog()
    {
        InitializeComponent();

        var confirm = this.FindControl<Button>("ConfirmButton");
        if (confirm is not null)
            confirm.Click += OnConfirmClick;

        var cancel = this.FindControl<Button>("CancelButton");
        if (cancel is not null)
            cancel.Click += OnCancelClick;
    }

    internal async Task<bool?> ShowStandaloneAsync()
    {
        var completion = new TaskCompletionSource<bool?>();
        void OnClosed(object? sender, EventArgs e) => completion.TrySetResult(_result);
        Closed += OnClosed;
        try
        {
            WindowStartupLocation = WindowStartupLocation.CenterScreen;
            Show();
            return await completion.Task;
        }
        finally
        {
            Closed -= OnClosed;
        }
    }

    private void OnConfirmClick(object? sender, RoutedEventArgs e)
    {
        _result = true;
        Close(true);
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        _result = false;
        Close(false);
    }
}
