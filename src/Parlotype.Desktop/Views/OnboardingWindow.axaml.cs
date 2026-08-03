using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Parlotype.Desktop.Onboarding;
using Parlotype.Desktop.ViewModels.Onboarding;

namespace Parlotype.Desktop.Views;

/// <summary>
/// The onboarding tour window (ADR-056): a frameless Topmost card that shows
/// the current step's text and, per step, waits for the step's target window
/// to appear, moves itself next to it, and applies the element highlights.
/// The view owns presentation only — step state lives in
/// <see cref="OnboardingWizardViewModel"/>, highlight mechanics in
/// <see cref="OnboardingHighlightService"/>.
/// </summary>
public partial class OnboardingWindow : Window
{
    /// <summary>Gap between the wizard card and the target window, in DIPs.</summary>
    private const int PlacementGap = 16;

    /// <summary>How long to wait for a step's target window to become visible.</summary>
    private static readonly TimeSpan TargetWindowTimeout = TimeSpan.FromSeconds(2);

    private OnboardingWizardViewModel? _viewModel;
    private int _presentVersion;

    public OnboardingWindow()
    {
        InitializeComponent();
        DataContextChanged += (_, _) => AttachViewModel(DataContext as OnboardingWizardViewModel);
        Opened += (_, _) => _ = PresentStepAsync();
        Closed += (_, _) =>
        {
            HighlightService?.Clear();
            AttachViewModel(null);
        };
    }

    /// <summary>
    /// Set by <c>OnboardingService</c> before the window is shown. The window
    /// is constructed in code (not via DI), so this is plain property injection.
    /// </summary>
    public OnboardingHighlightService? HighlightService { get; set; }

    private void InitializeComponent() => AvaloniaXamlLoader.Load(this);

    private void AttachViewModel(OnboardingWizardViewModel? viewModel)
    {
        if (_viewModel is not null)
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _viewModel = viewModel;
        if (_viewModel is not null)
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(OnboardingWizardViewModel.CurrentIndex))
            _ = PresentStepAsync();
    }

    /// <summary>
    /// Presents the current step: clears old highlights, waits for the step's
    /// target window (WindowManager shows it via a fire-and-forget dispatcher
    /// post, so there is no completion signal to await), then repositions this
    /// card beside it and applies the highlights. Superseded presentations
    /// (the user clicked Next again) abort at the version check.
    /// </summary>
    private async Task PresentStepAsync()
    {
        var version = ++_presentVersion;
        HighlightService?.Clear();

        var step = _viewModel?.CurrentStep;
        if (step is null || !IsVisible)
            return;

        if (step.TargetWindow == OnboardingTargetWindow.None)
        {
            // Text-only step — keep whatever position we have.
            FocusNextButton();
            return;
        }

        // `IWindowManager` shows windows from a Normal-priority dispatcher post,
        // so when the target is *already* open the search below would succeed
        // synchronously and we would activate ourselves before that post runs —
        // and `SettingsWindow.Activate()` would then take the keyboard straight
        // back. Yielding below Normal first makes the post run before we do.
        await Dispatcher.UIThread.InvokeAsync(() => { }, DispatcherPriority.Background);
        if (version != _presentVersion)
            return;

        var target = await WaitForTargetWindowAsync(step.TargetWindow, version);
        if (target is null || version != _presentVersion)
            return;

        PositionRelativeTo(target);
        HighlightService?.Apply(target, step.TargetIds);

        // The Transcribe widget is Topmost too and the Settings window activates
        // itself when shown; re-activate so the wizard stays readable above them,
        // and take the keyboard back so the tour stays drivable from Enter.
        Activate();
        FocusNextButton();
    }

    /// <summary>
    /// Puts keyboard focus on Next. <see cref="NavigationMethod.Tab"/> so the
    /// focus ring is actually drawn — the user has to be able to see that Enter
    /// will advance.
    /// </summary>
    private void FocusNextButton() =>
        this.FindControl<Button>("NextButton")?.Focus(NavigationMethod.Tab);

    private async Task<Window?> WaitForTargetWindowAsync(OnboardingTargetWindow kind, int version)
    {
        var deadline = DateTime.UtcNow + TargetWindowTimeout;
        while (DateTime.UtcNow < deadline && version == _presentVersion)
        {
            var target = FindTargetWindow(kind);
            if (target is not null)
                return target;

            await Task.Delay(50);
        }

        return FindTargetWindow(kind);
    }

    private static Window? FindTargetWindow(OnboardingTargetWindow kind)
    {
        if (Application.Current?.ApplicationLifetime is not IClassicDesktopStyleApplicationLifetime desktop)
            return null;

        return kind switch
        {
            OnboardingTargetWindow.Transcribe =>
                desktop.Windows.OfType<TranscribeWindow>().FirstOrDefault(w => w.IsVisible),
            OnboardingTargetWindow.Settings =>
                desktop.Windows.OfType<SettingsWindow>().FirstOrDefault(w => w.IsVisible),
            _ => null,
        };
    }

    /// <summary>
    /// Places the card to the right of the target window (tops roughly
    /// aligned), flipping to the left when the working area has no room, and
    /// clamping into the target's screen either way.
    /// </summary>
    private void PositionRelativeTo(Window target)
    {
        var screen = Screens?.ScreenFromWindow(target) ?? Screens?.Primary;
        if (screen is null)
            return; // headless — leave the position alone

        var area = screen.WorkingArea;
        var scaling = target.DesktopScaling;
        var targetSize = PixelSize.FromSize(target.Bounds.Size, scaling);
        var mySize = PixelSize.FromSize(Bounds.Size, DesktopScaling);
        var gap = (int)(PlacementGap * scaling);

        var x = target.Position.X + targetSize.Width + gap;
        if (x + mySize.Width > area.Right)
            x = target.Position.X - mySize.Width - gap;
        x = Math.Clamp(x, area.X, Math.Max(area.X, area.Right - mySize.Width));

        var y = Math.Clamp(target.Position.Y, area.Y, Math.Max(area.Y, area.Bottom - mySize.Height));

        Position = new PixelPoint(x, y);
    }

    private void DragZone_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginMoveDrag(e);
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        if (e.Key == Key.Escape)
        {
            e.Handled = true;
            _viewModel?.SkipCommand.Execute(null);
            return;
        }

        base.OnKeyDown(e);
    }
}
