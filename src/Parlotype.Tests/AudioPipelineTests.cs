using Parlotype.Core.Audio;
using Parlotype.Core.Speech;
using Parlotype.Platform.Audio;
using Parlotype.Platform.Speech;
using Whisper.net.Ggml;
using Xunit;

namespace Parlotype.Tests;

public class AudioPipelineTests
{
    [Fact]
    public async Task Pipeline_WithVadAndWhisper_ProducesTranscription()
    {
        // Arrange: create a mock capture service that feeds the WAV file
        var capture = new TestAudioCaptureService();
        await using var vad = new SileroVadService();
        await using var recognizer = new WhisperSpeechRecognizer(GgmlType.Tiny);

        await using var pipeline = new AudioPipelineService(capture, vad, recognizer);

        TranscriptionResult? transcription = null;
        var transcriptionReceived = new TaskCompletionSource<TranscriptionResult>();

        pipeline.TranscriptionAvailable += (_, e) =>
        {
            transcription = e.Result;
            transcriptionReceived.TrySetResult(e.Result);
        };

        // Act: start pipeline and feed audio
        await pipeline.StartAsync(PipelineMode.Batch);

        var pcmBytes = TestAudioHelper.LoadWavAsPcmBytes("one-small-step.wav");
        capture.SimulateAudioData(pcmBytes);

        // Add silence to trigger end-of-speech detection
        capture.SimulateAudioData(new byte[16_000 * 2]); // 1 second silence

        // Wait for transcription (timeout after 60 seconds)
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        cts.Token.Register(() => transcriptionReceived.TrySetCanceled());

        var result = await transcriptionReceived.Task;

        await pipeline.StopAsync();

        // Assert
        Assert.NotNull(result);
        Assert.False(string.IsNullOrWhiteSpace(result.Text));
        Assert.True(result.Text.Trim().Length > 5,
            $"Expected meaningful transcription but got: '{result.Text}'");
    }
}

/// <summary>Test double for IAudioCaptureService that allows manual audio injection.</summary>
internal sealed class TestAudioCaptureService : IAudioCaptureService
{
    public bool IsCapturing { get; private set; }

    public event EventHandler<AudioDataEventArgs>? DataAvailable;

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        IsCapturing = true;
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken = default)
    {
        IsCapturing = false;
        return Task.CompletedTask;
    }

    /// <summary>Simulates audio data arriving from a microphone.</summary>
    public void SimulateAudioData(byte[] pcmBytes)
    {
        DataAvailable?.Invoke(this, new AudioDataEventArgs
        {
            Buffer = pcmBytes,
            Format = AudioFormat.Whisper
        });
    }

    public ValueTask DisposeAsync()
    {
        IsCapturing = false;
        return ValueTask.CompletedTask;
    }
}
