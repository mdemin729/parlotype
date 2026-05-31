using Parlotype.Core.Speech;
using Xunit;

namespace Parlotype.Tests;

public class LanguageCatalogTests
{
    [Fact]
    public void WhisperLanguages_ContainsCommonLanguages()
    {
        var codes = LanguageCatalog.WhisperLanguages.Select(l => l.Code).ToHashSet();

        Assert.Contains("en", codes);
        Assert.Contains("ru", codes);
        Assert.Contains("fr", codes);
        Assert.Contains("de", codes);
        // Whisper-specific code not present in standard culture data.
        Assert.Contains("yue", codes);
    }

    [Fact]
    public void WhisperLanguages_HaveUniqueCodes()
    {
        var codes = LanguageCatalog.WhisperLanguages.Select(l => l.Code).ToList();
        Assert.Equal(codes.Count, codes.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void AllLanguages_IsLargerThanWhisperSet_AndHasNames()
    {
        Assert.True(LanguageCatalog.AllLanguages.Count > LanguageCatalog.WhisperLanguages.Count);
        Assert.All(LanguageCatalog.AllLanguages, l =>
        {
            Assert.False(string.IsNullOrWhiteSpace(l.Code));
            Assert.False(string.IsNullOrWhiteSpace(l.EnglishName));
            Assert.False(string.IsNullOrWhiteSpace(l.NativeName));
        });
    }

    [Theory]
    [InlineData("ru", "Russian")]
    [InlineData("fr", "French")]
    public void GetEnglishName_KnownCode_ReturnsName(string code, string expected)
    {
        Assert.Equal(expected, LanguageCatalog.GetEnglishName(code));
    }

    [Fact]
    public void GetEnglishName_UnknownCode_ReturnsCode()
    {
        Assert.Equal("zz", LanguageCatalog.GetEnglishName("zz"));
    }

    [Theory]
    [InlineData(null, true)]
    [InlineData("", true)]
    [InlineData("auto", true)]
    [InlineData("AUTO", true)]
    [InlineData("en", false)]
    public void IsAutoDetect_RecognisesSentinel(string? code, bool expected)
    {
        Assert.Equal(expected, LanguageCatalog.IsAutoDetect(code));
    }

    [Theory]
    [InlineData("none", true)]
    [InlineData("", true)]
    [InlineData("ru", false)]
    public void IsNoTranslation_RecognisesSentinel(string? code, bool expected)
    {
        Assert.Equal(expected, LanguageCatalog.IsNoTranslation(code));
    }
}

public class LanguageCapabilitiesTests
{
    [Fact]
    public void Whisper_UsesFixedSet_AndNoArbitraryTranslation()
    {
        var caps = SpeechEngineCapabilities.For(SpeechEngine.Whisper);

        Assert.True(caps.SupportsAutoDetect);
        Assert.False(caps.SupportsArbitraryTranslation);
        Assert.Same(LanguageCatalog.WhisperLanguages, caps.SupportedSourceLanguages);
        Assert.Same(LanguageCatalog.WhisperLanguages, caps.EffectiveSourceLanguages);
    }

    [Fact]
    public void Gemma4_UsesFullList_AndArbitraryTranslation()
    {
        var caps = SpeechEngineCapabilities.For(SpeechEngine.Gemma4);

        Assert.True(caps.SupportsAutoDetect);
        Assert.True(caps.SupportsArbitraryTranslation);
        Assert.Null(caps.SupportedSourceLanguages);
        Assert.Same(LanguageCatalog.AllLanguages, caps.EffectiveSourceLanguages);
    }
}

public class RecentLanguagesTests
{
    [Fact]
    public void Add_MovesCodeToFront()
    {
        var result = RecentLanguages.Add(["ru", "fr"], "fr");
        Assert.Equal(["fr", "ru"], result);
    }

    [Fact]
    public void Add_DedupesCaseInsensitively()
    {
        var result = RecentLanguages.Add(["ru", "FR"], "fr");
        Assert.Equal(["fr", "ru"], result);
    }

    [Fact]
    public void Add_CapsAtMax()
    {
        var result = RecentLanguages.Add(["a", "b", "c", "d", "e"], "f", max: 5);
        Assert.Equal(["f", "a", "b", "c", "d"], result);
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("none")]
    [InlineData("")]
    [InlineData(null)]
    public void Add_IgnoresSentinelsAndBlanks(string? code)
    {
        var result = RecentLanguages.Add(["ru"], code);
        Assert.Equal(["ru"], result);
    }

    [Fact]
    public void Add_NullExisting_StartsFresh()
    {
        var result = RecentLanguages.Add(null, "ru");
        Assert.Equal(["ru"], result);
    }
}
