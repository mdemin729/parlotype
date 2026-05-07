using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using Parlotype.Core.Audio;

namespace Parlotype.Desktop.Views;

/// <summary>
/// Custom control that visualises three recording states:
/// <list type="bullet">
///   <item><see cref="RecordingState.Disabled"/> — static microphone icon</item>
///   <item><see cref="RecordingState.Idle"/> — gently breathing bars (silence)</item>
///   <item><see cref="RecordingState.Active"/> — animated multi-frequency wave (speech)</item>
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

    /// <summary>Blend factor 0.0 (idle) → 1.0 (active), animated per tick for smooth transitions.</summary>
    private double _activeBlend;

    /// <summary>Blend change per frame (~16ms). 0.06 ≈ 300ms transition at 60fps.</summary>
    private const double BlendSpeed = 0.06;

    private const int BarCount = 13;

    // Brushes resolved from theme resources, with fallbacks
    private static readonly IBrush FallbackActiveBrush = new SolidColorBrush(Color.Parse("#378ADD"));
    private static readonly IBrush FallbackIdleBrush = new SolidColorBrush(Color.Parse("#B4B2A9"));
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

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (State == RecordingState.Disabled)
            return;

        // Animate blend factor toward target (1.0 for Active, 0.0 for Idle)
        var target = State == RecordingState.Active ? 1.0 : 0.0;
        if (_activeBlend < target)
            _activeBlend = Math.Min(_activeBlend + BlendSpeed, 1.0);
        else if (_activeBlend > target)
            _activeBlend = Math.Max(_activeBlend - BlendSpeed, 0.0);

        // Phase speed interpolates between idle (slow) and active (fast)
        _phase += 0.015 + _activeBlend * (0.06 - 0.015);

        InvalidateVisual();
    }

    public override void Render(DrawingContext ctx)
    {
        switch (State)
        {
            case RecordingState.Disabled:
                RenderMicIcon(ctx);
                break;
            case RecordingState.Idle:
                RenderBars(ctx, idle: true);
                break;
            case RecordingState.Active:
                RenderBars(ctx, idle: false);
                break;
        }
    }

    private IBrush ResolveBrush(string resourceKey, IBrush fallback)
    {
        if (this.TryFindResource(resourceKey, ActualThemeVariant, out var resource) && resource is IBrush brush)
            return brush;
        return fallback;
    }

    private void RenderBars(DrawingContext ctx, bool idle)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var maxBarH = h * 0.95;
        var barW = w / (BarCount * 1.8);
        var totalW = BarCount * barW * 1.8;
        var offsetX = (w - totalW) / 2;

        // Lerp brush color during transition
        var blend = _activeBlend;
        var brush = blend < 0.01
            ? ResolveBrush("WaveformIdleBrush", FallbackIdleBrush)
            : ResolveBrush("WaveformActiveBrush", FallbackActiveBrush);

        for (int i = 0; i < BarCount; i++)
        {
            // Compute both idle and active bar heights, then lerp
            var idleH = maxBarH * (0.10 + 0.04 * Math.Sin(_phase + i * 0.4));

            var amp = AudioAmplitude > 0.01f ? AudioAmplitude : 0.6f;
            var wave = Math.Sin(_phase * 1.7 + i * 0.55) * 0.45
                     + Math.Sin(_phase * 2.9 + i * 0.35) * 0.30
                     + Math.Sin(_phase * 0.8 + i * 0.90) * 0.25;
            var activeH = maxBarH * (0.12 + 0.88 * Math.Abs(wave) * amp);

            var barH = idleH + (activeH - idleH) * blend;
            barH = Math.Max(barH, 4);

            var x = offsetX + i * barW * 1.8;
            var y = (h - barH) / 2;
            var rx = barW / 2;
            var rect = new Rect(x, y, barW, barH);
            ctx.DrawRectangle(brush, null, rect, rx, rx);
        }
    }

    private void RenderMicIcon(DrawingContext ctx)
    {
        var brush = ResolveBrush("WaveformDisabledBrush", FallbackDisabledBrush);
        var pen = new Pen(brush, 2.5, lineCap: PenLineCap.Round);
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;

        // Mic body
        var micGeo = new StreamGeometry();
        using (var gc = micGeo.Open())
        {
            gc.BeginFigure(new Point(cx - 7, cy - 12), true);
            gc.ArcTo(new Point(cx + 7, cy - 12), new Size(7, 7), 0, false, SweepDirection.Clockwise);
            gc.LineTo(new Point(cx + 7, cy + 6));
            gc.ArcTo(new Point(cx - 7, cy + 6), new Size(7, 7), 0, false, SweepDirection.Clockwise);
            gc.EndFigure(true);
        }
        ctx.DrawGeometry(brush, null, micGeo);

        const int radii = 10;
        // Arc below mic
        var arcGeo = new StreamGeometry();
        using (var gc = arcGeo.Open())
        {
            gc.BeginFigure(new Point(cx - radii, cy + 6), false);
            gc.ArcTo(new Point(cx + radii, cy + 6), new Size(radii, radii), 0, false, SweepDirection.CounterClockwise);
        }
        ctx.DrawGeometry(null, pen, arcGeo);

        // Stem + base line
        ctx.DrawLine(pen, new Point(cx, cy + 16), new Point(cx, cy + 22));
        ctx.DrawLine(pen, new Point(cx - 7, cy + 22), new Point(cx + 7, cy + 22));
    }
}
