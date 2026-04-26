using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Parlotype.WinUI.ViewModels;
using Windows.Graphics;

namespace Parlotype.WinUI.Views;

/// <summary>
/// A compact, chromeless floating window for transcription control.
/// </summary>
public sealed partial class TranscribeWindow : Window
{
    public TranscribeViewModel ViewModel { get; }

    public TranscribeWindow(TranscribeViewModel viewModel)
    {
        ViewModel = viewModel;

        InitializeComponent();

        // Extend content into the title bar for a chromeless look.
        ExtendsContentIntoTitleBar = true;
        SetTitleBar(AppTitleBar);

        // Apply Mica backdrop (falls back to Acrylic if unavailable).
        SystemBackdrop = new MicaBackdrop();

        // Resize to a compact floating pill.
        AppWindow.Resize(new SizeInt32(300, 200));

        // Centre on the primary display.
        CenterOnScreen();
    }

    private void CenterOnScreen()
    {
        var displayArea = DisplayArea.GetFromWindowId(
            AppWindow.Id, DisplayAreaFallback.Primary);

        if (displayArea is null)
            return;

        var workArea = displayArea.WorkArea;
        var size = AppWindow.Size;

        var x = (workArea.Width - size.Width) / 2 + workArea.X;
        var y = (workArea.Height - size.Height) / 2 + workArea.Y;

        AppWindow.Move(new PointInt32(x, y));
    }
}
