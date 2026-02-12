using Parlotype.Core.Audio;
using Xunit;

namespace Parlotype.Tests;

public class SampleTest
{
    [Fact]
    public void AudioFormat_Whisper_HasExpectedValues()
    {
        var format = AudioFormat.Whisper;

        Assert.Equal(16_000, format.SampleRate);
        Assert.Equal(1, format.Channels);
        Assert.Equal(16, format.BitsPerSample);
    }
}
