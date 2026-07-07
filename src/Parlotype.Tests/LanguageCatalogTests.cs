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

    [Theory]
    [InlineData("keyboard", true)]
    [InlineData("KEYBOARD", true)]
    [InlineData("", false)]   // blank means auto, never keyboard — it's an explicit opt-in
    [InlineData(null, false)]
    [InlineData("en", false)]
    public void IsKeyboardLayout_RecognisesSentinel(string? code, bool expected)
    {
        Assert.Equal(expected, LanguageCatalog.IsKeyboardLayout(code));
    }

    [Fact]
    public void TryGet_KeyboardSentinel_ReturnsNull()
    {
        Assert.Null(LanguageCatalog.TryGet(LanguageCatalog.KeyboardLayoutCode));
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

    // Engine → target form map per §Spec 11: Whisper = Toggle, Gemma 4 = Full,
    // transcribe-only (no targets, no arbitrary) = None.
    [Fact]
    public void TranslationForm_Whisper_IsToggle()
    {
        Assert.Equal(TranslationForm.Toggle, SpeechEngineCapabilities.For(SpeechEngine.Whisper).TranslationForm);
    }

    [Fact]
    public void TranslationForm_Gemma4_IsFull()
    {
        Assert.Equal(TranslationForm.Full, SpeechEngineCapabilities.For(SpeechEngine.Gemma4).TranslationForm);
    }

    [Fact]
    public void Parakeet_UsesFixedSet_AndNoTranslation()
    {
        var caps = SpeechEngineCapabilities.For(SpeechEngine.Parakeet);

        Assert.True(caps.SupportsAutoDetect);
        Assert.False(caps.SupportsArbitraryTranslation);
        Assert.Same(LanguageCatalog.ParakeetLanguages, caps.SupportedSourceLanguages);
        Assert.Empty(caps.FixedTranslationTargets);
    }

    [Fact]
    public void TranslationForm_Parakeet_IsNone()
    {
        Assert.Equal(TranslationForm.None, SpeechEngineCapabilities.For(SpeechEngine.Parakeet).TranslationForm);
    }

    [Fact]
    public void ParakeetLanguages_Has25Entries_AllFromWhisperCatalog()
    {
        Assert.Equal(25, LanguageCatalog.ParakeetLanguages.Count);
        Assert.All(LanguageCatalog.ParakeetLanguages,
            l => Assert.Contains(l, LanguageCatalog.WhisperLanguages));
    }

    [Fact]
    public void ParakeetLanguages_StartsWithEnglish_AndHasNoDuplicates()
    {
        Assert.Equal("en", LanguageCatalog.ParakeetLanguages[0].Code);
        Assert.Equal(
            LanguageCatalog.ParakeetLanguages.Count,
            LanguageCatalog.ParakeetLanguages.Select(l => l.Code).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void TranslationForm_NoTargetsNoArbitrary_IsNone()
    {
        var caps = new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: null,
            SupportsArbitraryTranslation: false,
            FixedTranslationTargets: []);

        Assert.Equal(TranslationForm.None, caps.TranslationForm);
    }

    [Fact]
    public void TranslationForm_ArbitraryWinsOverFixedTargets()
    {
        var caps = new LanguageCapabilities(
            SupportsAutoDetect: true,
            SupportedSourceLanguages: null,
            SupportsArbitraryTranslation: true,
            FixedTranslationTargets: [LanguageCatalog.TryGet("en")!]);

        Assert.Equal(TranslationForm.Full, caps.TranslationForm);
    }
}

public class SourceLanguageResolverTests
{
    private static readonly KeyboardLayoutInfo EnglishUs = new("en", "English (United States)");

    [Fact]
    public void Resolve_ExplicitCode_PassesThrough()
    {
        Assert.Equal("ru", SourceLanguageResolver.Resolve("ru", EnglishUs));
    }

    [Fact]
    public void Resolve_AutoCode_PassesThrough()
    {
        Assert.Equal("auto", SourceLanguageResolver.Resolve("auto", EnglishUs));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void Resolve_BlankCode_FallsBackToAuto(string? code)
    {
        Assert.Equal(LanguageCatalog.AutoDetectCode, SourceLanguageResolver.Resolve(code, EnglishUs));
    }

    [Fact]
    public void Resolve_KeyboardSentinel_UsesDetectedLayout()
    {
        Assert.Equal("en", SourceLanguageResolver.Resolve("keyboard", EnglishUs));
    }

    [Fact]
    public void Resolve_KeyboardSentinel_NoDetection_FallsBackToAuto()
    {
        Assert.Equal(LanguageCatalog.AutoDetectCode, SourceLanguageResolver.Resolve("keyboard", null));
    }

    [Fact]
    public void Resolve_KeyboardSentinel_DetectedLanguageUnsupported_FallsBackToAuto()
    {
        // A layout language outside the engine's source list must not leak through.
        var layout = new KeyboardLayoutInfo("zz", "Imaginary (ZZ)");
        var result = SourceLanguageResolver.Resolve("keyboard", layout, LanguageCatalog.WhisperLanguages);

        Assert.Equal(LanguageCatalog.AutoDetectCode, result);
    }

    [Fact]
    public void Resolve_KeyboardSentinel_DetectedLanguageSupported_ChecksCaseInsensitively()
    {
        var layout = new KeyboardLayoutInfo("RU", "Russian (Russia)");
        var result = SourceLanguageResolver.Resolve("keyboard", layout, LanguageCatalog.WhisperLanguages);

        Assert.Equal("RU", result);
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
    [InlineData("keyboard")]
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
