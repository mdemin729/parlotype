using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Styling;
using Avalonia.VisualTree;
using Parlotype.Core.Audio;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class WaveformViewTests
{
    [AvaloniaTheory]
    [InlineData(false)]
    [InlineData(true)]
    public void RecordingWave_RendersSpeechAndSilenceInBothThemes(bool dark)
    {
        var vm = new TranscribeViewModel(new MockWindowManager())
        {
            IsRecording = true,
            RecordingState = RecordingState.Idle,
        };
        var window = new TranscribeWindow
        {
            DataContext = vm,
            RequestedThemeVariant = dark ? ThemeVariant.Dark : ThemeVariant.Light,
        };
        window.Show();
        try
        {
            var view = window.GetVisualDescendants().OfType<WaveformView>().Single();
            // Optional real-control frames for local visual review; no microphone needed.
            var output = Environment.GetEnvironmentVariable("PARLOTYPE_WAVEFORM_PREVIEW_DIRECTORY");
            if (output is not null) Directory.CreateDirectory(output);
            var frames = output is null ? 6 : 180;
            for (var frame = 0; frame < frames; frame++)
            {
                var time = frame * 6.0 / frames;
                var speaking = time is >= 1 and < 4;
                vm.RecordingState = speaking ? RecordingState.Active : RecordingState.Idle;
                vm.AudioLevel = speaking ? (float)(0.025 + 0.055 * Math.Pow(Math.Sin(time * 5), 2)) : 0;
                view.AdvanceAnimation(6.0 / frames);
                AvaloniaHeadlessPlatform.ForceRenderTimerTick();
                using var bitmap = window.CaptureRenderedFrame();
                Assert.NotNull(bitmap);
                if (output is not null)
                    bitmap.Save(Path.Combine(output, $"{(dark ? "dark" : "light")}-{frame:D3}.png"));
            }
        }
        finally
        {
            window.Close();
        }
    }

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

        // Exercise all recording states.
        view.State = RecordingState.Disabled;
        window.InvalidateVisual();

        view.State = RecordingState.Idle;
        window.InvalidateVisual();

        view.State = RecordingState.Loading;
        window.InvalidateVisual();

        view.State = RecordingState.Active;
        view.AudioAmplitude = 0.5f;
        window.InvalidateVisual();

        window.Close();
    }
}
