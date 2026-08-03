using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;

namespace Parlotype.Desktop.Onboarding;

/// <summary>
/// The pulsing outline drawn over an onboarding target while its wizard step is
/// active (ADR-056). Attached via <c>AdornerLayer.SetAdorner</c>, so the
/// adorner layer keeps it aligned with the target through layout changes.
/// Animated the house way — <see cref="DispatcherTimer"/> + <see cref="Render"/>,
/// like <c>WaveformView</c> — with the timer stopped whenever the control
/// leaves the visual tree.
/// </summary>
public sealed class OnboardingHighlight : Control
{
    private const double PulsePeriodSeconds = 1.6;

    private static readonly Color AccentColor = Color.Parse("#378ADD");

    private readonly DispatcherTimer _timer;
    private double _phase;

    public OnboardingHighlight()
    {
        IsHitTestVisible = false;
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(80) };
        _timer.Tick += (_, _) =>
        {
            _phase += 0.08 / PulsePeriodSeconds * 2 * Math.PI;
            InvalidateVisual();
        };
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _timer.Stop();
    }

    public override void Render(DrawingContext context)
    {
        // Opacity swings 0.35..1.0 so the outline breathes instead of blinking.
        var opacity = 0.675 + 0.325 * Math.Sin(_phase);
        var pen = new Pen(new SolidColorBrush(AccentColor, opacity), 2);
        var rect = new Rect(Bounds.Size).Deflate(1);
        if (rect.Width <= 0 || rect.Height <= 0)
            return;

        context.DrawRectangle(null, pen, rect, 8, 8);
    }
}
