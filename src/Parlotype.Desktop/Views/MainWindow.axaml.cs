using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;

namespace Parlotype.Desktop.Views;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();

        var dragHandle = this.FindControl<Border>("DragHandle");
        if (dragHandle is not null)
        {
            dragHandle.PointerPressed += OnDragHandlePointerPressed;
        }

        var closeButton = this.FindControl<Button>("CloseButton");
        if (closeButton is not null)
        {
            closeButton.Click += OnCloseButtonClick;
        }
    }

    private void OnDragHandlePointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnCloseButtonClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
