using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The settings nav pane scrolls, and says so. Whisper shows the longest nav
/// list (four engine-specific pages on top of the shared ones); stacked in a
/// non-scrolling panel it simply ran off the bottom of the pane, hiding Data and
/// Help with nothing on screen to suggest they existed.
/// </summary>
public class SettingsWindowNavScrollTests
{
    /// <summary>Short enough that the nav list cannot fit, whatever the engine.</summary>
    private const int ShortWindow = 420;

    private static (SettingsWindow Window, ListBox Nav, ScrollViewer Scroll) ShowWindow(int height) =>
        ShowWindow(height, out _);

    private static (SettingsWindow Window, ListBox Nav, ScrollViewer Scroll) ShowWindow(
        int height, out Parlotype.Desktop.ViewModels.SettingsWindowViewModel shellOut)
    {
        var shell = SettingsWindowViewModelFactory.Build();
        shellOut = shell;
        shell.SpeechEngine.SelectedEngine = Core.Speech.SpeechEngine.Whisper;

        var window = new SettingsWindow { DataContext = shell, Height = height };
        window.Show();
        Dispatcher.UIThread.RunJobs();

        var nav = window.FindControl<ListBox>("NavList");
        Assert.NotNull(nav);
        var scroll = Assert.IsType<ScrollViewer>(nav!.Scroll);
        return (window, nav!, scroll);
    }

    [AvaloniaFact]
    public void NavList_Scrolls_WhenRowsOutgrowThePane()
    {
        var (window, _, scroll) = ShowWindow(ShortWindow);

        Assert.True(
            scroll.Extent.Height > scroll.Viewport.Height,
            $"Nav rows ({scroll.Extent.Height}px) should overflow the {scroll.Viewport.Height}px "
            + "viewport and scroll, not be clipped away.");
        Assert.True(scroll.Offset.Y == 0);

        window.Close();
    }

    [AvaloniaFact]
    public void NavList_FadesTheEdgeThatHidesRows()
    {
        var (window, nav, scroll) = ShowWindow(ShortWindow);

        // Parked at the top: only the bottom edge has rows behind it.
        Assert.Equal(Mask(window, "NavFadeBottomMask"), nav.OpacityMask);

        // Mid-list: both edges do.
        ScrollTo(scroll, (scroll.Extent.Height - scroll.Viewport.Height) / 2);
        Assert.Equal(Mask(window, "NavFadeBothMask"), nav.OpacityMask);

        // Bottom: only the top edge does.
        ScrollTo(scroll, double.MaxValue);
        Assert.Equal(Mask(window, "NavFadeTopMask"), nav.OpacityMask);

        window.Close();
    }

    [AvaloniaFact]
    public void NavList_IsUnmasked_WhenEveryRowFits()
    {
        var (window, nav, scroll) = ShowWindow(1400);

        Assert.True(scroll.Extent.Height <= scroll.Viewport.Height);
        Assert.Null(nav.OpacityMask);

        window.Close();
    }

    [AvaloniaFact]
    public void NavList_ScrollsADeepLinkedRowIntoView()
    {
        var (window, _, scroll) = ShowWindow(ShortWindow, out var shell);
        Assert.Equal(0, scroll.Offset.Y);

        // Help is the last row, well below the fold on a short window.
        shell.SelectedNavItem = shell.NavItems.Last(n => !n.IsHeader);
        Dispatcher.UIThread.RunJobs();

        Assert.True(
            scroll.Offset.Y > 0,
            "Selecting a section from outside the nav (the Transcribe strip deep-links "
            + "into Help and the language pages) has to bring its row into view.");

        window.Close();
    }

    [AvaloniaFact]
    public void NavList_ScrollsADeepLinkedRowIntoView_WhenAHiddenWindowIsShownAgain()
    {
        var (window, nav, scroll) = ShowWindow(ShortWindow, out var shell);

        // The settings window is reused: closing it only hides it
        // (WindowManager.ShowSettings), and the deep link is applied before the
        // window comes back, so the selection changes while nothing is laid out.
        window.Hide();
        Dispatcher.UIThread.RunJobs();

        shell.NavigateTo(Parlotype.Desktop.ViewModels.SettingsSection.Help);
        Dispatcher.UIThread.RunJobs();

        window.Show();
        Dispatcher.UIThread.RunJobs();

        Assert.True(
            scroll.Offset.Y > 0,
            "Reopening on a deep-linked section must show that section's row, not "
            + "leave the list parked at the offset it had when it was hidden.");

        // The fades follow the jump. Which of the two top-fading masks is in
        // play depends on whether ScrollIntoView also cleared the list's bottom
        // padding, which is not the point of this test.
        Assert.True(
            nav.OpacityMask == Mask(window, "NavFadeTopMask")
            || nav.OpacityMask == Mask(window, "NavFadeBothMask"),
            "Rows are now hidden above, so the top edge has to fade.");

        window.Close();
    }

    /// <summary>Scrolls to <paramref name="y"/> (clamped) and lets layout settle.</summary>
    private static void ScrollTo(ScrollViewer scroll, double y)
    {
        scroll.Offset = new Vector(0, y);
        Dispatcher.UIThread.RunJobs();
    }

    private static IBrush? Mask(SettingsWindow window, string key) =>
        window.TryFindResource(key, out var brush) ? brush as IBrush : null;
}
