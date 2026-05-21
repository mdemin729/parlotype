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
        var closeButton = this.FindControl<Button>("CloseButton");

        if (downloadButton is not null)
            downloadButton.Click += OnDownloadClick;
        if (cancelButton is not null)
            cancelButton.Click += OnCancelClick;
        if (closeButton is not null)
            closeButton.Click += OnCloseClick;
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

    private void OnCloseClick(object? sender, RoutedEventArgs e)
    {
        // Reached only after a successful download — keep UserConfirmed true.
        Close();
    }
}
