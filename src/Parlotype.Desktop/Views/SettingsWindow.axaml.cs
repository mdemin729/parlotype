using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;

namespace Parlotype.Desktop.Views;

/// <summary>
/// Settings shell: nav pane on the left, the selected section on the right.
/// </summary>
/// <remarks>
/// The nav pane scrolls (see the DockPanel note in the markup), and this
/// code-behind supplies the affordance that says so: the list fades out against
/// whichever edge still has rows behind it. Avalonia has no binding-friendly
/// "can scroll up/down" state, so the fades are driven from the list's own
/// <see cref="ScrollViewer"/> here rather than from the view model - this is
/// view state, and the shell view model knows nothing about viewports.
/// </remarks>
public partial class SettingsWindow : Window
{
    /// <summary>Sub-pixel slack, so a rounded offset doesn't fade a list that is fully scrolled.</summary>
    private const double EdgeSlack = 1.0;

    private ListBox? _navList;
    private ScrollViewer? _navScroll;

    public SettingsWindow()
    {
        InitializeComponent();
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        // The window is reused: closing it only hides it, and WindowManager
        // deep-links (SettingsWindowViewModel.NavigateTo) *before* showing it
        // again. Nothing is laid out while hidden, so the ListBox's own
        // AutoScrollToSelectedItem quietly does nothing and the pane comes back
        // parked at the offset it had when it went away - with, say, Help
        // selected and only the top rows on screen. Re-assert it whenever the
        // window becomes visible.
        if (change.Property == IsVisibleProperty && change.GetNewValue<bool>())
            BringSelectedRowIntoView();
    }

    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        _navList = this.FindControl<ListBox>("NavList");
        if (_navList is null)
            return;

        // Bubbles from the list's template ScrollViewer, and fires for extent
        // and viewport changes too - so switching engines (which adds or drops
        // nav rows) and resizing the window both re-evaluate the fades.
        _navList.AddHandler(ScrollViewer.ScrollChangedEvent, OnNavScrollChanged);

        // The template isn't applied yet on Loaded; the first layout pass raises
        // ScrollChanged, but post an update as well so a pane that never scrolls
        // still settles into a defined state.
        Dispatcher.UIThread.Post(UpdateNavFade, DispatcherPriority.Loaded);
    }

    private void OnNavScrollChanged(object? sender, ScrollChangedEventArgs e) => UpdateNavFade();

    /// <summary>
    /// Scrolls the selected nav row into view once the pane has been laid out.
    /// Queued at <see cref="DispatcherPriority.Loaded"/> because visibility
    /// flips before the layout pass, and scrolling an unmeasured list is a no-op.
    /// </summary>
    private void BringSelectedRowIntoView() => Dispatcher.UIThread.Post(
        () =>
        {
            // May run before OnLoaded on the window's first appearance.
            _navList ??= this.FindControl<ListBox>("NavList");
            if (_navList?.SelectedItem is { } selected)
                _navList.ScrollIntoView(selected);
        },
        DispatcherPriority.Loaded);

    /// <summary>
    /// Masks the nav list so it dissolves towards any edge with rows hidden
    /// beyond it, and leaves it unmasked when everything already fits.
    /// </summary>
    private void UpdateNavFade()
    {
        if (_navList is null)
            return;

        _navScroll ??= _navList.Scroll as ScrollViewer;
        if (_navScroll is null)
            return;

        var hiddenAbove = _navScroll.Offset.Y > EdgeSlack;
        var hiddenBelow = _navScroll.Offset.Y + _navScroll.Viewport.Height
                          < _navScroll.Extent.Height - EdgeSlack;

        _navList.OpacityMask = (hiddenAbove, hiddenBelow) switch
        {
            (true, true) => FadeMask("NavFadeBothMask"),
            (true, false) => FadeMask("NavFadeTopMask"),
            (false, true) => FadeMask("NavFadeBottomMask"),
            _ => null,
        };
    }

    private IBrush? FadeMask(string key) =>
        this.TryFindResource(key, out var brush) ? brush as IBrush : null;
}
