using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Parlotype.Desktop.Views;

/// <summary>
/// Small modal confirm/cancel dialog. Shown via
/// <c>ShowDialog&lt;bool?&gt;(owner)</c>: Confirm closes with <c>true</c>,
/// Cancel (or closing the window) yields <c>null</c>, which callers treat as
/// a cancel. Content comes from <see cref="ViewModels.ConfirmationDialogViewModel"/>.
/// </summary>
public partial class ConfirmationDialog : Window
{
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

    private void OnConfirmClick(object? sender, RoutedEventArgs e) => Close(true);

    private void OnCancelClick(object? sender, RoutedEventArgs e) => Close(false);
}
