using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Parlotype.Core.Audio;

namespace Parlotype.Desktop.Views;

/// <summary>
/// Custom control that visualises recording states:
/// <list type="bullet">
///   <item><see cref="RecordingState.Disabled"/> — static microphone icon</item>
///   <item><see cref="RecordingState.Loading"/> — rotating arc spinner (model loading)</item>
///   <item><see cref="RecordingState.Idle"/> — stationary, softly faded dots (silence)</item>
///   <item><see cref="RecordingState.Active"/> — smooth audio-reactive wave (speech)</item>
/// </list>
/// </summary>
public class WaveformView : Control
{
    public static readonly StyledProperty<RecordingState> StateProperty =
        AvaloniaProperty.Register<WaveformView, RecordingState>(nameof(State));

    public static readonly StyledProperty<float> AudioAmplitudeProperty =
        AvaloniaProperty.Register<WaveformView, float>(nameof(AudioAmplitude));

    public RecordingState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    public float AudioAmplitude
    {
        get => GetValue(AudioAmplitudeProperty);
        set => SetValue(AudioAmplitudeProperty, value);
    }

    private double _phase;
    private DispatcherTimer? _timer;

    private readonly WaveformAnimation _animation = new();
    private long _lastFrame;

    // Brushes resolved from theme resources, with fallbacks
    private static readonly IBrush FallbackActiveBrush = new SolidColorBrush(Color.Parse("#378ADD"));
    private static readonly IBrush FallbackDisabledBrush = new SolidColorBrush(Color.Parse("#378ADD"));

    static WaveformView()
    {
        AffectsRender<WaveformView>(StateProperty, AudioAmplitudeProperty);
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timer.Tick += OnTimerTick;
        _lastFrame = Stopwatch.GetTimestamp();
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_timer is not null)
        {
            _timer.Tick -= OnTimerTick;
            _timer.Stop();
            _timer = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == StateProperty && State is RecordingState.Disabled or RecordingState.Loading)
            _animation.Reset();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        var now = Stopwatch.GetTimestamp();
        var elapsed = Stopwatch.GetElapsedTime(_lastFrame, now).TotalSeconds;
        _lastFrame = now;
        AdvanceAnimation(elapsed);
    }

    internal void AdvanceAnimation(double elapsed)
    {
        if (State == RecordingState.Disabled)
            return;

        _phase += elapsed;
        _animation.Advance(elapsed, State == RecordingState.Active ? AudioAmplitude : 0);
        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        switch (State)
        {
            case RecordingState.Disabled:
                RenderMicIcon(ctx);
                break;
            case RecordingState.Loading:
                RenderSpinner(ctx);
                break;
            case RecordingState.Idle:
            case RecordingState.Active:
                RenderBars(ctx);
                break;
        }
    }

    private IBrush ResolveBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;
        return fallback;
    }

    private void RenderBars(DrawingContext ctx)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        if (w <= 0 || h <= 0)
            return;

        const int count = WaveformAnimation.BarCount;
        var barW = w / (count * 1.6);
        var spacing = (w - barW) / (count - 1);
        var restHeight = Math.Min(barW, h);
        var brush = ResolveBrush("WaveformActiveBrush", FallbackActiveBrush);

        for (var i = 0; i < count; i++)
        {
            var edge = Math.Abs(i - (count - 1) / 2.0) / ((count - 1) / 2.0);
            var energy = _animation.GetHeight(i);
            var barH = restHeight + (h * 0.9 - restHeight) * energy;
            var rect = new Rect(i * spacing, (h - barH) / 2, barW, barH);
            // Fine rounded strokes, a luminous center and quiet translucent edges.
            using (ctx.PushOpacity(0.48 + 0.42 * (1 - edge * edge) + 0.10 * energy))
                ctx.DrawRectangle(brush, null, rect, barW / 2, barW / 2);
        }
    }

    /// <summary>
    /// Draws a rotating arc spinner while the speech model loads. Uses the shared
    /// animation phase so it spins smoothly on the existing 16ms timer.
    /// </summary>
    private void RenderSpinner(DrawingContext ctx)
    {
        var brush = ResolveBrush("WaveformActiveBrush", FallbackActiveBrush);
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;
        var r = Math.Min(Bounds.Width, Bounds.Height) * 0.30;

        // Rotation derived from the shared phase (faster for a lively spinner).
        var rotation = _phase * 5.0;
        const double sweep = Math.PI * 1.5; // 270° arc
        var a0 = rotation;
        var a1 = rotation + sweep;

        var p0 = new Point(cx + r * Math.Cos(a0), cy + r * Math.Sin(a0));
        var p1 = new Point(cx + r * Math.Cos(a1), cy + r * Math.Sin(a1));

        var geo = new StreamGeometry();
        using (var gc = geo.Open())
        {
            gc.BeginFigure(p0, false);
            gc.ArcTo(p1, new Size(r, r), 0, isLargeArc: sweep > Math.PI, SweepDirection.Clockwise);
        }

        var pen = new Pen(brush, 4, lineCap: PenLineCap.Round);
        ctx.DrawGeometry(null, pen, geo);
    }

    private void RenderMicIcon(DrawingContext ctx)
    {
        var brush = ResolveBrush("WaveformDisabledBrush", FallbackDisabledBrush);
        var pen = new Pen(brush, 2.5, lineCap: PenLineCap.Round);
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;

        // Mic body
        var micGeo = new StreamGeometry();
        var topRadius = 5;
        using (var gc = micGeo.Open())
        {
            gc.BeginFigure(new Point(cx - topRadius, cy - topRadius - 5), true);
            gc.ArcTo(new Point(cx + topRadius, cy - topRadius - 5), new Size(topRadius, topRadius), 0, false, SweepDirection.Clockwise);
            gc.LineTo(new Point(cx + topRadius, cy + topRadius - 1));
            gc.ArcTo(new Point(cx - topRadius, cy + topRadius - 1), new Size(topRadius, topRadius), 0, false, SweepDirection.Clockwise);
            gc.EndFigure(true);
        }
        ctx.DrawGeometry(brush, null, micGeo);

        int radii = topRadius + 3;
        // Arc below mic
        var arcGeo = new StreamGeometry();
        using (var gc = arcGeo.Open())
        {
            gc.BeginFigure(new Point(cx - radii, cy + topRadius - 1), false);
            gc.ArcTo(new Point(cx + radii, cy + topRadius - 1), new Size(radii, radii), 0, false, SweepDirection.CounterClockwise);
        }
        ctx.DrawGeometry(null, pen, arcGeo);

        // Stem + base line
        var baseY = cy + topRadius + 13;
        ctx.DrawLine(pen, new Point(cx, cy + topRadius + 9), new Point(cx, baseY));
        ctx.DrawLine(pen, new Point(cx - topRadius, baseY), new Point(cx + topRadius, baseY));
    }
}
