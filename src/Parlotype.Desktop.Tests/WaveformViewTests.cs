using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Parlotype.Core.Audio;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class WaveformViewTests
{
    [AvaloniaFact]
    public void DefaultState_IsDisabled()
    {
        var view = new WaveformView();
        Assert.Equal(RecordingState.Disabled, view.State);
    }

    [AvaloniaFact]
    public void StateProperty_CanBeSet()
    {
        var view = new WaveformView();

        view.State = RecordingState.Idle;
        Assert.Equal(RecordingState.Idle, view.State);

        view.State = RecordingState.Active;
        Assert.Equal(RecordingState.Active, view.State);
    }

    [AvaloniaFact]
    public void AudioAmplitudeProperty_CanBeSet()
    {
        var view = new WaveformView();

        view.AudioAmplitude = 0.75f;
        Assert.Equal(0.75f, view.AudioAmplitude);
    }

    [AvaloniaFact]
    public void Render_DoesNotThrow_InAnyState()
    {
        var window = new Window
        {
            Width = 200,
            Height = 100
        };

        var view = new WaveformView { Width = 100, Height = 50 };
        window.Content = view;
        window.Show();

        // Exercise all three states — each triggers a different render path
        view.State = RecordingState.Disabled;
        window.InvalidateVisual();

        view.State = RecordingState.Idle;
        window.InvalidateVisual();

        view.State = RecordingState.Active;
        view.AudioAmplitude = 0.5f;
        window.InvalidateVisual();

        window.Close();
    }
}
