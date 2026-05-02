using Avalonia.Controls;
using Avalonia.Interactivity;

namespace Parlotype.Desktop.Views;

public partial class ModelDownloadDialog : Window
{
    /// <summary>True if the user clicked Download, false if cancelled.</summary>
    public bool UserConfirmed { get; private set; }

    public ModelDownloadDialog()
    {
        InitializeComponent();

        var downloadButton = this.FindControl<Button>("DownloadButton");
        var cancelButton = this.FindControl<Button>("CancelButton");

        if (downloadButton is not null)
            downloadButton.Click += OnDownloadClick;
        if (cancelButton is not null)
            cancelButton.Click += OnCancelClick;
    }

    private void OnDownloadClick(object? sender, RoutedEventArgs e)
    {
        UserConfirmed = true;
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        UserConfirmed = false;
        Close();
    }
}
