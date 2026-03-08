using Parlotype.Core.Audio;
using Xunit;

namespace Parlotype.Tests;

public class SpeechSegmentExtractorTests
{
    [Fact]
    public void Extract_EmptySegments_ReturnsEmptyArray()
    {
        var buffer = new float[16_000];
        var segments = new List<VadSpeechSegment>();

        var result = SpeechSegmentExtractor.Extract(buffer, segments);

        Assert.Empty(result);
    }

    [Fact]
    public void Extract_SingleSegment_NoSilenceInserted()
    {
        // Single segment: no gap needed (gap is only between segments)
        var buffer = Enumerable.Range(0, 16_000).Select(i => (float)i).ToArray();
        var segments = new List<VadSpeechSegment> { new(1000, 5000) };

        var result = SpeechSegmentExtractor.Extract(buffer, segments, interSegmentSilenceMs: 160);

        Assert.Equal(4000, result.Length);
        Assert.Equal(1000f, result[0]);
        Assert.Equal(4999f, result[^1]);
    }

    [Fact]
    public void Extract_TwoSegments_InsertsSilenceBetween()
    {
        var buffer = Enumerable.Range(0, 16_000).Select(i => (float)i).ToArray();
        var segments = new List<VadSpeechSegment>
        {
            new(0, 1000),   // 1000 samples
            new(5000, 6000) // 1000 samples
        };

        // 160ms at 16kHz = 2560 samples of silence
        var result = SpeechSegmentExtractor.Extract(buffer, segments, interSegmentSilenceMs: 160);

        int expectedLength = 1000 + 2560 + 1000;
        Assert.Equal(expectedLength, result.Length);

        // First segment data
        Assert.Equal(0f, result[0]);
        Assert.Equal(999f, result[999]);

        // Silence region should be all zeros
        for (int i = 1000; i < 1000 + 2560; i++)
        {
            Assert.Equal(0f, result[i]);
        }

        // Second segment data
        Assert.Equal(5000f, result[1000 + 2560]);
        Assert.Equal(5999f, result[^1]);
    }

    [Fact]
    public void Extract_ThreeSegments_InsertsSilenceBetweenEachPair()
    {
        var buffer = Enumerable.Range(0, 16_000).Select(i => 1.0f).ToArray();
        var segments = new List<VadSpeechSegment>
        {
            new(0, 100),
            new(500, 600),
            new(1000, 1100)
        };

        int silenceSamples = 100 * 16_000 / 1000; // 100ms = 1600 samples
        var result = SpeechSegmentExtractor.Extract(buffer, segments, interSegmentSilenceMs: 100);

        // 3 segments of 100 samples each + 2 gaps of 1600 samples each
        int expectedLength = 3 * 100 + 2 * silenceSamples;
        Assert.Equal(expectedLength, result.Length);
    }

    [Fact]
    public void Extract_ZeroSilenceMs_ConcatenatesDirectly()
    {
        var buffer = Enumerable.Range(0, 16_000).Select(i => (float)i).ToArray();
        var segments = new List<VadSpeechSegment>
        {
            new(0, 1000),
            new(5000, 6000)
        };

        var result = SpeechSegmentExtractor.Extract(buffer, segments, interSegmentSilenceMs: 0);

        // No silence gap — direct concatenation (old behavior)
        Assert.Equal(2000, result.Length);
        Assert.Equal(0f, result[0]);
        Assert.Equal(999f, result[999]);
        Assert.Equal(5000f, result[1000]);
        Assert.Equal(5999f, result[^1]);
    }

    [Fact]
    public void Extract_SegmentClampedToBufferLength()
    {
        var buffer = new float[500];
        Array.Fill(buffer, 1.0f);
        var segments = new List<VadSpeechSegment> { new(0, 10_000) }; // EndSample far beyond buffer

        var result = SpeechSegmentExtractor.Extract(buffer, segments);

        Assert.Equal(500, result.Length);
        Assert.All(result, sample => Assert.Equal(1.0f, sample));
    }

    [Fact]
    public void Extract_SilenceRegionContainsZeros()
    {
        // Fill buffer with non-zero values to verify silence is zero, not copied from buffer
        var buffer = new float[16_000];
        Array.Fill(buffer, 42.0f);
        var segments = new List<VadSpeechSegment>
        {
            new(0, 100),
            new(200, 300)
        };

        var result = SpeechSegmentExtractor.Extract(buffer, segments, interSegmentSilenceMs: 100);
        int silenceSamples = 100 * 16_000 / 1000; // 1600

        // Verify silence gap contains zeros
        for (int i = 100; i < 100 + silenceSamples; i++)
        {
            Assert.Equal(0f, result[i]);
        }

        // Verify speech regions contain the original value
        Assert.Equal(42.0f, result[0]);
        Assert.Equal(42.0f, result[100 + silenceSamples]);
    }
}
