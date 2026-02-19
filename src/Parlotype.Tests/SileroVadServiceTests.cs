using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Platform.Audio;
using Xunit;

namespace Parlotype.Tests;

public class SileroVadServiceTests
{
    [Fact]
    public async Task DetectSpeech_WithSpeechAudio_ReturnsSpeechSegments()
    {
        await using var vad = new SileroVadService(NullLogger<SileroVadService>.Instance);
        var samples = TestAudioHelper.LoadWavAsFloatSamples("kennedy.wav");

        var segments = vad.DetectSpeech(samples);

        Assert.NotEmpty(segments);
        Assert.All(segments, s =>
        {
            Assert.True(s.StartSample >= 0);
            Assert.True(s.EndSample > s.StartSample);
            Assert.True(s.EndSample <= samples.Length);
        });
    }

    [Fact]
    public async Task DetectSpeech_WithSilence_ReturnsNoSegments()
    {
        await using var vad = new SileroVadService(NullLogger<SileroVadService>.Instance);
        var silence = new float[16_000]; // 1 second of silence

        var segments = vad.DetectSpeech(silence);

        Assert.Empty(segments);
    }
}
