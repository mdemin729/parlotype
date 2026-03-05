using Parlotype.Benchmark.Metrics;

namespace Parlotype.Benchmark.Tests;

public sealed class TextNormalizerTests
{
    [Fact]
    public void Normalize_LowercasesText()
    {
        Assert.Equal("hello world", TextNormalizer.Normalize("Hello World"));
    }

    [Fact]
    public void Normalize_RemovesPunctuation()
    {
        Assert.Equal("hello world", TextNormalizer.Normalize("Hello, World!"));
    }

    [Fact]
    public void Normalize_CollapsesWhitespace()
    {
        Assert.Equal("hello world", TextNormalizer.Normalize("hello   world"));
    }

    [Fact]
    public void Normalize_TrimsText()
    {
        Assert.Equal("hello world", TextNormalizer.Normalize("  hello world  "));
    }

    [Fact]
    public void Normalize_HandlesEmpty()
    {
        Assert.Equal(string.Empty, TextNormalizer.Normalize(""));
        Assert.Equal(string.Empty, TextNormalizer.Normalize(null));
        Assert.Equal(string.Empty, TextNormalizer.Normalize("   "));
    }

    [Fact]
    public void Normalize_PreservesDigits()
    {
        Assert.Equal("test 123 value", TextNormalizer.Normalize("Test 123 Value"));
    }

    [Fact]
    public void Normalize_HandlesComplexPunctuation()
    {
        Assert.Equal("it s a dog s life", TextNormalizer.Normalize("It's a dog's life!"));
    }
}
