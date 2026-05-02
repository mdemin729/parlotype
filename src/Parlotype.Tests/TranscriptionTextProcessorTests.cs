using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class TranscriptionTextProcessorTests
{
    [Theory]
    [InlineData("Hello, world.", "Hello world")]
    [InlineData("Wait... what?", "Wait what")]
    [InlineData("Yes! No.", "Yes No")]
    [InlineData("A; B: C", "A B C")]
    public void StripPunctuation_RemovesSentencePunctuation(string input, string expected)
    {
        var processor = new TranscriptionTextProcessor(stripPunctuation: true, filterProfanity: false);
        Assert.Equal(expected, processor.Process(input));
    }

    [Theory]
    [InlineData("don't", "don't")]
    [InlineData("it's a co-op", "it's a co-op")]
    [InlineData("3.14 is pi", "3.14 is pi")]
    public void StripPunctuation_PreservesIntraWordPunctuation(string input, string expected)
    {
        var processor = new TranscriptionTextProcessor(stripPunctuation: true, filterProfanity: false);
        Assert.Equal(expected, processor.Process(input));
    }

    [Fact]
    public void FilterProfanity_MasksProfaneWords()
    {
        var processor = new TranscriptionTextProcessor(stripPunctuation: false, filterProfanity: true);
        var result = processor.Process("What the fuck is going on");
        Assert.Equal("What the **** is going on", result);
    }

    [Fact]
    public void FilterProfanity_IsCaseInsensitive()
    {
        var processor = new TranscriptionTextProcessor(stripPunctuation: false, filterProfanity: true);
        var result = processor.Process("DAMN that was loud");
        Assert.Equal("**** that was loud", result);
    }

    [Fact]
    public void FilterProfanity_WholeWordOnly_NoFalsePositives()
    {
        var processor = new TranscriptionTextProcessor(stripPunctuation: false, filterProfanity: true);
        // "class" contains "ass" but should not be censored
        var result = processor.Process("This is a class assignment");
        Assert.Equal("This is a class assignment", result);
    }

    [Fact]
    public void BothEnabled_AppliesBothTransformations()
    {
        var processor = new TranscriptionTextProcessor(stripPunctuation: true, filterProfanity: true);
        // Punctuation stripped first, then profanity masked
        var result = processor.Process("Damn, that's bad!");
        Assert.Equal("**** that's bad", result);
    }

    [Fact]
    public void NeitherEnabled_ReturnsOriginal()
    {
        var processor = new TranscriptionTextProcessor(stripPunctuation: false, filterProfanity: false);
        Assert.Equal("Hello, world!", processor.Process("Hello, world!"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void EmptyOrWhitespace_ReturnsAsIs(string? input)
    {
        var processor = new TranscriptionTextProcessor(stripPunctuation: true, filterProfanity: true);
        Assert.Equal(input, processor.Process(input!));
    }
}
