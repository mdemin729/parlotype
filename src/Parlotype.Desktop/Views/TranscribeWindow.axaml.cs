using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Parlotype.Core.Settings;
using Parlotype.Desktop.ViewModels;

namespace Parlotype.Desktop.Views;

/// <summary>
/// Frameless compact transcribe widget (ADR-040). Dragging is confined to the
/// grip strip at the top; the ✕ button and Esc hide the window to the tray.
/// The user-chosen position is persisted (via <see cref="IWindowStateService"/>,
/// kept separate from long-lived settings) so the widget reappears where it was
/// left, falling back to center-screen when the saved point is off-screen.
/// </summary>
public partial class TranscribeWindow : Window
{
    /// <summary>Height with the language quick-picker strip (ADR-040 design C2).</summary>
    private const double FullHeight = 118;

    /// <summary>Height without the strip — engines with no language choices (Parakeet).</summary>
    private const double CompactHeight = 88;

    private IWindowStateService? _positionStore;
    private TranscribeViewModel? _viewModel;

    public TranscribeWindow()
    {
        InitializeComponent();
        Opened += (_, _) => (DataContext as TranscribeViewModel)?.Relationship?.BeginLivePolling();
        Closed += (_, _) => (DataContext as TranscribeViewModel)?.Relationship?.EndLivePolling();
        // WindowManager cancels Closing and hides instead; persist before either.
        Closing += (_, _) => _ = SavePositionAsync();
        DataContextChanged += (_, _) => AttachViewModel(DataContext as TranscribeViewModel);
    }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void AttachViewModel(TranscribeViewModel? viewModel)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = viewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;

        UpdateHeightForStrip();
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(TranscribeViewModel.HasLanguageStrip))
            UpdateHeightForStrip();
    }

    /// <summary>
    /// The widget is fixed-size (no SizeToContent, ADR-040), so hiding the
    /// language strip must shrink the window explicitly or it leaves a blank
    /// band at the bottom.
    /// </summary>
    private void UpdateHeightForStrip() =>
        Height = _viewModel?.HasLanguageStrip == true ? FullHeight : CompactHeight;

    /// <summary>
    /// Wires position persistence and applies the saved position when it is
    /// still visible on a connected screen. Call before the first Show().
    /// </summary>
    public async Task RestorePositionAsync(IWindowStateService windowState)
    {
        _positionStore = windowState;

        var saved = await windowState.GetAsync<WindowPosition?>(WindowStateKeys.TranscribeWindowPosition);
        if (saved is not { } position)
            return;

        var pixelPosition = new PixelPoint(position.X, position.Y);
        if (!IsOnAnyScreen(pixelPosition))
            return; // monitor layout changed — keep the CenterScreen default

        WindowStartupLocation = WindowStartupLocation.Manual;
        Position = pixelPosition;
    }

    /// <summary>Persists the current position; no-op until restore has run.</summary>
    public Task SavePositionAsync()
    {
        if (_positionStore is null)
            return Task.CompletedTask;

        return _positionStore.SetAsync(WindowStateKeys.TranscribeWindowPosition,
            new WindowPosition(Position.X, Position.Y));
    }

    private bool IsOnAnyScreen(PixelPoint position)
    {
        var screens = Screens?.All;
        if (screens is null || screens.Count == 0)
            return true; // no screen info (headless/tests) — trust the saved point

        var size = PixelSize.FromSize(new Size(Width, Height), DesktopScaling);
        var windowRect = new PixelRect(position, size);
        return screens.Any(s => s.Bounds.Intersects(windowRect));
    }

    private void GripZone_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // On Windows this enters the modal move loop and returns when the drag
        // ends, so the position saved afterwards is the drop position.
        BeginMoveDrag(e);
        _ = SavePositionAsync();
    }

    private void CloseButton_Click(object? sender, RoutedEventArgs e) => HideToTray();

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            HideToTray();
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Hides the widget without stopping recording; the tray reopens it.</summary>
    private void HideToTray()
    {
        _ = SavePositionAsync();
        Hide();
    }
}
