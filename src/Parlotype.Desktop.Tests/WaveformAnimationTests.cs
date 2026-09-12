using Parlotype.Desktop.Views;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class WaveformAnimationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(0.003)]
    [InlineData(float.NaN)]
    [InlineData(float.PositiveInfinity)]
    public void SilenceNoiseAndInvalidSamples_StayAtRest(float level)
    {
        var animation = new WaveformAnimation();
        animation.Advance(1, level);
        Assert.All(Heights(animation), height => Assert.Equal(0, height));
    }

    [Fact]
    public void Silence_SettlesPromptlyAndRemainsStill()
    {
        var animation = new WaveformAnimation();
        animation.Advance(1, 1);
        Assert.Contains(Heights(animation), h => h > 0.1);
        animation.Advance(0.4, 0);
        Assert.All(Heights(animation), h => Assert.InRange(h, 0, 0.015));
        animation.Advance(0.4, 0);
        Assert.All(Heights(animation), h => Assert.Equal(0, h));
        animation.Advance(1, 0);
        Assert.All(Heights(animation), h => Assert.Equal(0, h));
    }

    [Fact]
    public void LevelCrossingOldFallbackThreshold_HasNoAmplitudeJump()
    {
        var low = new WaveformAnimation();
        var high = new WaveformAnimation();
        low.Advance(1, 0.00999f);
        high.Advance(1, 0.01001f);
        for (var i = 0; i < WaveformAnimation.BarCount; i++)
            Assert.InRange(Math.Abs(low.GetHeight(i) - high.GetHeight(i)), 0, 0.001);
    }

    [Fact]
    public void SuddenLoudInput_DoesNotJumpToFullHeight()
    {
        var animation = new WaveformAnimation();
        animation.Advance(1.0 / 60, 1);
        Assert.All(Heights(animation), h => Assert.InRange(h, 0, 0.06));
        animation.Advance(0.3, 1);
        Assert.Contains(Heights(animation), h => h > 0.2);
        animation.Reset();
        Assert.All(Heights(animation), h => Assert.Equal(0, h));
    }

    [Fact]
    public void DifferentFrameRates_ProduceTheSameResponse()
    {
        var slow = new WaveformAnimation();
        var fast = new WaveformAnimation();
        for (var i = 0; i < 30; i++) slow.Advance(1.0 / 30, 0.05f);
        for (var i = 0; i < 120; i++) fast.Advance(1.0 / 120, 0.05f);
        for (var i = 0; i < WaveformAnimation.BarCount; i++)
            Assert.Equal(slow.GetHeight(i), fast.GetHeight(i), 6);
    }

    private static IEnumerable<double> Heights(WaveformAnimation animation)
        => Enumerable.Range(0, WaveformAnimation.BarCount).Select(animation.GetHeight);
}
