using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Media.Imaging;
using Avalonia.Threading;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// Renders Avalonia controls in a headless window and captures screenshots as base64 PNG.
/// Must be called from a test marked with <c>[AvaloniaFact]</c>.
/// </summary>
public static class ScreenshotHelper
{
    /// <summary>
    /// Renders the given control inside a headless window and returns its screenshot as a base64 PNG string.
    /// Uses Avalonia's headless <c>CaptureRenderedFrame</c> for proper rendering.
    /// The window is sized to fit the full content height (no clipping).
    /// </summary>
    public static async Task<string> CaptureBase64Async(
        Control control,
        object? dataContext = null,
        int width = 600,
        int maxHeight = 1200)
    {
        if (dataContext is not null)
            control.DataContext = dataContext;

        // Start with a tall window so content isn't clipped during layout
        var window = new Window
        {
            Width = width,
            Height = maxHeight,
            Content = control,
        };

        window.Show();

        // Let layout settle and bindings propagate
        await Task.Delay(150);
        Dispatcher.UIThread.RunJobs();

        // Shrink window to actual content height for a tight screenshot
        var contentHeight = control.DesiredSize.Height;
        if (contentHeight > 20)
        {
            // Add padding for window chrome
            window.Height = Math.Min(contentHeight + 40, maxHeight);
            await Task.Delay(50);
            Dispatcher.UIThread.RunJobs();
        }

        var bitmap = window.CaptureRenderedFrame();
        if (bitmap is null)
        {
            window.Close();
            return string.Empty;
        }

        using var ms = new MemoryStream();
        bitmap.Save(ms);
        var base64 = Convert.ToBase64String(ms.ToArray());

        window.Close();
        return base64;
    }
}
