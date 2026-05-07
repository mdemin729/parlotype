Now here's how to implement all three states cleanly in Avalonia.

---

## Architecture: a single `WaveformView` control with a `RecordingState` enum

```csharp
public enum RecordingState { Disabled, Idle, Active }
```

---

## Option 1: Custom Control with `DrawingContext` (Recommended)

This gives you the most control over the wave shape and animation timing — critical for audio-responsive visuals.

```csharp
public class WaveformView : Control
{
    public static readonly StyledProperty<RecordingState> StateProperty =
        AvaloniaProperty.Register<WaveformView, RecordingState>(nameof(State));

    public RecordingState State
    {
        get => GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private double _phase = 0;
    private DispatcherTimer? _timer;

    // Plug in real audio amplitude here (0.0 – 1.0)
    public float AudioAmplitude { get; set; } = 0f;

    private static readonly IBrush ActiveBrush = new SolidColorBrush(Color.Parse("#378ADD"));
    private static readonly IBrush IdleBrush   = new SolidColorBrush(Color.Parse("#B4B2A9"));
    private static readonly IBrush DisabledBrush = new SolidColorBrush(Color.Parse("#E24B4A"));

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) }; // ~60fps
        _timer.Tick += (_, _) =>
        {
            if (State != RecordingState.Disabled)
                _phase += State == RecordingState.Active ? 0.06 : 0.015;
            InvalidateVisual();
        };
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        _timer?.Stop();
        base.OnDetachedFromVisualTree(e);
    }

    public override void Render(DrawingContext ctx)
    {
        switch (State)
        {
            case RecordingState.Disabled: RenderMicIcon(ctx); break;
            case RecordingState.Idle:     RenderBars(ctx, idle: true);  break;
            case RecordingState.Active:   RenderBars(ctx, idle: false); break;
        }
    }

    private const int BarCount = 13;

    private void RenderBars(DrawingContext ctx, bool idle)
    {
        var w = Bounds.Width;
        var h = Bounds.Height;
        var maxBarH = h * 0.88;
        var barW = w / (BarCount * 1.8);
        var totalW = BarCount * barW * 1.8;
        var offsetX = (w - totalW) / 2;
        var brush = idle ? IdleBrush : ActiveBrush;

        for (int i = 0; i < BarCount; i++)
        {
            double barH;
            if (idle)
            {
                barH = maxBarH * (0.10 + 0.04 * Math.Sin(_phase + i * 0.4));
            }
            else
            {
                // Multi-frequency wave — replace with real FFT data for audio-reactive
                var amp = AudioAmplitude > 0.01f ? AudioAmplitude : 0.6f;
                var wave = Math.Sin(_phase * 1.7 + i * 0.55) * 0.45
                         + Math.Sin(_phase * 2.9 + i * 0.35) * 0.30
                         + Math.Sin(_phase * 0.8 + i * 0.90) * 0.25;
                barH = maxBarH * (0.12 + 0.88 * Math.Abs(wave) * amp);
            }
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
        // Use a cached Geometry or a PathGeometry built from StreamGeometry
        // For a real app: load from an embedded SVG resource or use PathIcon
        var pen = new Pen(DisabledBrush, 2.5, lineCap: PenLineCap.Round);
        var cx = Bounds.Width / 2;
        var cy = Bounds.Height / 2;

        // Mic body (rounded rect approximated with arcs)
        var micGeo = new StreamGeometry();
        using (var gc = micGeo.Open())
        {
            gc.BeginFigure(new Point(cx - 7, cy - 12), true);
            gc.ArcTo(new Point(cx + 7, cy - 12), new Size(7, 7), 0, false, SweepDirection.Clockwise);
            gc.LineTo(new Point(cx + 7, cy + 6));
            gc.ArcTo(new Point(cx - 7, cy + 6), new Size(7, 7), 0, false, SweepDirection.Clockwise);
            gc.EndFigure(true);
        }
        ctx.DrawGeometry(DisabledBrush, null, micGeo);

        // Arc below mic
        var arcGeo = new StreamGeometry();
        using (var gc = arcGeo.Open())
        {
            gc.BeginFigure(new Point(cx - 13, cy + 2), false);
            gc.ArcTo(new Point(cx + 13, cy + 2), new Size(13, 13), 0, false, SweepDirection.Clockwise);
        }
        ctx.DrawGeometry(null, pen, arcGeo);

        // Stem + base line
        ctx.DrawLine(pen, new Point(cx, cy + 15), new Point(cx, cy + 22));
        ctx.DrawLine(pen, new Point(cx - 7, cy + 22), new Point(cx + 7, cy + 22));

        // Diagonal "disabled" slash
        ctx.DrawLine(new Pen(DisabledBrush, 2.5, lineCap: PenLineCap.Round),
            new Point(cx - 14, cy - 14), new Point(cx + 14, cy + 14));
    }
}
```

---

## Option 2: Audio-Reactive Bars (Whisper.net + NAudio)

Since Parlotype already captures audio, feed real RMS amplitude directly into the bars instead of the simulated wave:

```csharp
// In your AudioCaptureService, publish amplitude events
public float ComputeRms(float[] samples)
{
    var sum = samples.Sum(s => s * s);
    return (float)Math.Sqrt(sum / samples.Length);
}

// Bind to WaveformView
_audioCaptureService.AmplitudeChanged += amp =>
{
    Dispatcher.UIThread.Post(() => waveformView.AudioAmplitude = amp);
};
```

---

## XAML Integration

```xml
<local:WaveformView
    Width="160" Height="56"
    State="{Binding RecordingState}"
    ClipToBounds="False"/>
```

Bind `RecordingState` from your ViewModel and the control handles all three visual states automatically, including the timer starting and stopping with the visual tree.

---

## State Transition: Add CSS Animation for the Switch

Wrap the control in a `Border` and animate `Opacity` on state change for a clean crossfade:

```xml
<Border>
  <Border.Styles>
    <Style Selector="Border">
      <Setter Property="Opacity" Value="1"/>
      <Style.Transitions>
        <Transitions>
          <DoubleTransition Property="Opacity" Duration="0:0:0.25"/>
        </Transitions>
      </Style.Transitions>
    </Style>
  </Border.Styles>
  <local:WaveformView State="{Binding RecordingState}"/>
</Border>
```

---

## Summary

| Aspect | Recommendation |
|---|---|
| Rendering | Custom `Control` + `DrawingContext` |
| Animation driver | `DispatcherTimer` at 60fps |
| Audio reactivity | Replace sine wave with real RMS from NAudio buffer |
| State transitions | `Opacity` CSS Transition on a wrapper `Border` |
| Mic icon | `StreamGeometry` in `RenderMicIcon`, or `PathIcon` from resource |
| SVG | Skip — overkill here, Avalonia.Svg adds a dependency for what you can draw in 30 lines |

The `AudioAmplitude` property is the key hook — once you wire it to real microphone input from your existing VAD pipeline, the bars will react to the user's actual voice with no additional logic needed.