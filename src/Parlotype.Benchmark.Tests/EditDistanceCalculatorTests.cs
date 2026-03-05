using Parlotype.Benchmark.Metrics;

namespace Parlotype.Benchmark.Tests;

public sealed class EditDistanceCalculatorTests
{
    [Fact]
    public void ComputeWer_PerfectMatch_ReturnsZero()
    {
        Assert.Equal(0.0, EditDistanceCalculator.ComputeWer("the cat sat on the mat", "the cat sat on the mat"));
    }

    [Fact]
    public void ComputeWer_SingleSubstitution()
    {
        // "the cat sat on the mat" (6 words), "the cat sat on the bat" (1 substitution)
        // WER = 1/6 * 100 = 16.67%
        var wer = EditDistanceCalculator.ComputeWer("the cat sat on the mat", "the cat sat on the bat");
        Assert.InRange(wer, 16.6, 16.7);
    }

    [Fact]
    public void ComputeWer_Insertion()
    {
        // Reference: "the cat" (2 words), Hypothesis: "the big cat" (1 insertion)
        // WER = 1/2 * 100 = 50%
        var wer = EditDistanceCalculator.ComputeWer("the cat", "the big cat");
        Assert.Equal(50.0, wer);
    }

    [Fact]
    public void ComputeWer_Deletion()
    {
        // Reference: "the big cat" (3 words), Hypothesis: "the cat" (1 deletion)
        // WER = 1/3 * 100 = 33.33%
        var wer = EditDistanceCalculator.ComputeWer("the big cat", "the cat");
        Assert.InRange(wer, 33.3, 33.4);
    }

    [Fact]
    public void ComputeWer_EmptyReference_EmptyHypothesis_ReturnsZero()
    {
        Assert.Equal(0.0, EditDistanceCalculator.ComputeWer("", ""));
    }

    [Fact]
    public void ComputeWer_EmptyReference_NonEmptyHypothesis_Returns100()
    {
        Assert.Equal(100.0, EditDistanceCalculator.ComputeWer("", "some text"));
    }

    [Fact]
    public void ComputeWer_CompletelyWrong()
    {
        // Reference: "hello world" (2 words), Hypothesis: "goodbye earth" (2 substitutions)
        // WER = 2/2 * 100 = 100%
        var wer = EditDistanceCalculator.ComputeWer("hello world", "goodbye earth");
        Assert.Equal(100.0, wer);
    }

    [Fact]
    public void ComputeWer_IgnoresCaseAndPunctuation()
    {
        Assert.Equal(0.0, EditDistanceCalculator.ComputeWer(
            "Hello, World!", "hello world"));
    }

    [Fact]
    public void ComputeCer_PerfectMatch_ReturnsZero()
    {
        Assert.Equal(0.0, EditDistanceCalculator.ComputeCer("hello", "hello"));
    }

    [Fact]
    public void ComputeCer_SingleCharacterDifference()
    {
        // "cat" (3 chars) vs "bat" (1 substitution) → CER = 1/3 * 100 = 33.33%
        var cer = EditDistanceCalculator.ComputeCer("cat", "bat");
        Assert.InRange(cer, 33.3, 33.4);
    }

    [Fact]
    public void ComputeCer_EmptyBoth_ReturnsZero()
    {
        Assert.Equal(0.0, EditDistanceCalculator.ComputeCer("", ""));
    }

    [Fact]
    public void ComputeEditOps_ReturnsCorrectBreakdown()
    {
        var reference = new[] { "the", "cat", "sat" };
        var hypothesis = new[] { "the", "dog", "sat" };

        var (s, d, i, n) = EditDistanceCalculator.ComputeEditOps(reference, hypothesis);

        Assert.Equal(1, s); // "cat" → "dog" substitution
        Assert.Equal(0, d);
        Assert.Equal(0, i);
        Assert.Equal(3, n);
    }
}
