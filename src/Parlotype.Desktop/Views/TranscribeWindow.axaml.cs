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
/// Also raises <see cref="UserEngaged"/> and exposes the fade used by
/// <see cref="Services.TranscribeAutoHideController"/> to auto-hide a
/// dictation-summoned window once it settles (ADR-068).
/// </summary>
public partial class TranscribeWindow : Window
{
    /// <summary>Height with the language quick-picker strip (ADR-040 design C2).</summary>
    private const double FullHeight = 118;

    /// <summary>Height without the strip — engines with no language choices (Parakeet).</summary>
    private const double CompactHeight = 88;

    private IWindowStateService? _positionStore;
    private TranscribeViewModel? _viewModel;
    private readonly Border? _rootChrome;

    /// <summary>The in-flight auto-hide fade, if any; non-null only between
    /// <see cref="HideWithFadeAsync"/> starting and its <c>finally</c> running.</summary>
    private CancellationTokenSource? _fade;

    /// <summary>
    /// Raised when the user does something committal to the widget — a click
    /// anywhere, which includes the grip drag and every button. Promotes an
    /// auto-summoned window to one the user owns for the rest of its visible
    /// life (ADR-068, FR-5). Window *activation* is deliberately not another
    /// source of this event — see <see cref="Services.TranscribeAutoHideController"/>.
    /// </summary>
    public event EventHandler? UserEngaged;

    /// <summary>Duration of the auto-hide fade (ADR-068, FR-10). Settable so
    /// tests can shrink it instead of waiting out a real animation.</summary>
    public TimeSpan FadeDuration { get; set; } = TimeSpan.FromMilliseconds(160);

    public TranscribeWindow()
    {
        InitializeComponent();
        _rootChrome = this.FindControl<Border>("RootChrome");

        // Tunnel so UserEngaged fires before any child handles or marks the
        // event handled — GripZone_PointerPressed and every button keep
        // working exactly as before (ADR-068).
        AddHandler(
            InputElement.PointerPressedEvent,
            (_, _) => UserEngaged?.Invoke(this, EventArgs.Empty),
            RoutingStrategies.Tunnel);

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

            // While recording, Escape discards the take rather than hiding the
            // widget — the same meaning it has in macOS Dictation. The global
            // hook handles this when the widget isn't focused.
            if (DataContext is TranscribeViewModel { IsRecording: true } vm)
                _ = vm.CancelRecordingAsync();
            else
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

    /// <summary>
    /// Hides the widget with a short fade (ADR-068, FR-10). Input is disabled
    /// for the duration so a card on its way out cannot swallow a click.
    /// Abortable: a dictation that restarts (or any other engagement) mid-fade
    /// calls <see cref="AbortFade"/> and the card is restored rather than
    /// hidden. A no-op if the window is already hidden or a fade is already
    /// running — <see cref="Services.TranscribeAutoHideController"/> is the
    /// only caller and never needs two fades stacked.
    /// </summary>
    public async Task HideWithFadeAsync()
    {
        if (!IsVisible || _fade is not null)
            return;

        var fade = new CancellationTokenSource();
        _fade = fade;
        try
        {
            IsHitTestVisible = false;
            if (_rootChrome is not null)
                _rootChrome.Opacity = 0;

            await Task.Delay(FadeDuration, fade.Token);
            HideToTray();
        }
        catch (OperationCanceledException)
        {
            // Aborted — fall through to the restore below rather than hiding.
        }
        finally
        {
            _fade = null;
            fade.Dispose();
            // Covers both outcomes: a completed hide must not leave the next
            // Show() starting from a transparent card, and an aborted fade
            // must not leave the card sitting at Opacity 0 while still
            // visible. WindowManager also calls this before every Show() as
            // a second line of defence (ADR-068).
            RestoreChrome();
        }
    }

    /// <summary>Cancels an in-flight fade; the window stays visible and
    /// <see cref="RestoreChrome"/> runs from <see cref="HideWithFadeAsync"/>'s
    /// <c>finally</c> (ADR-068).</summary>
    public void AbortFade() => _fade?.Cancel();

    /// <summary>Restores full opacity and re-enables input. Idempotent, so it is
    /// safe to call both from the fade's own cleanup and from WindowManager
    /// before every <c>Show()</c> (ADR-068).</summary>
    public void RestoreChrome()
    {
        IsHitTestVisible = true;
        if (_rootChrome is not null)
            _rootChrome.Opacity = 1;
    }
}
