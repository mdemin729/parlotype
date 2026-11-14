using Parlotype.Core.Speech;
using Parlotype.Core.TextProcessing;
using Xunit;

namespace Parlotype.Tests.TextProcessing;

/// <summary>
/// The output language speech cleanup plans on (ADR-075): what each engine
/// will write under the stored language settings, and how a transcript is
/// judged when the settings do not say.
/// </summary>
public sealed class CleanupLanguagePolicyTests
{
    private static readonly KeyboardLayoutInfo RussianLayout = new("ru", "Russian");
    private static readonly KeyboardLayoutInfo EnglishLayout = new("en", "English");

    private static CleanupLanguage Resolve(
        SpeechEngine engine,
        string? source = null,
        bool translate = false,
        string? target = null,
        WhisperModelType model = WhisperModelType.Base,
        KeyboardLayoutInfo? layout = null) =>
        CleanupLanguagePolicy.Resolve(engine, model, source, layout, translate, target);

    // --- Engines that ignore the language settings ---

    [Theory]
    [InlineData(SpeechEngine.Parakeet)]
    [InlineData(SpeechEngine.OpenAiCompatible)]
    [InlineData(SpeechEngine.XaiGrok)]
    public void AutoDetectOnlyEngines_AreUnknown_WhateverIsStored(SpeechEngine engine)
    {
        // Left over from a Whisper session; these engines never read them (ADR-042).
        var language = Resolve(engine, source: "ru", translate: true, target: "de");

        Assert.Equal(CleanupLanguage.Unknown, language);
    }

    // --- Whisper ---

    [Fact]
    public void Whisper_ExplicitEnglishSource_IsEnglish() =>
        Assert.Equal(CleanupLanguage.English, Resolve(SpeechEngine.Whisper, source: "en"));

    [Fact]
    public void Whisper_ExplicitOtherSource_IsThatLanguage()
    {
        var language = Resolve(SpeechEngine.Whisper, source: "ru");

        Assert.Equal(CleanupLanguageKind.NotEnglish, language.Kind);
        Assert.Equal("ru", language.Code);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(LanguageCatalog.AutoDetectCode)]
    public void Whisper_AutoDetect_IsUnknown(string? source) =>
        Assert.Equal(CleanupLanguage.Unknown, Resolve(SpeechEngine.Whisper, source: source));

    [Fact]
    public void Whisper_TranslatingToEnglish_IsEnglish_WhateverIsSpoken() =>
        Assert.Equal(
            CleanupLanguage.English,
            Resolve(SpeechEngine.Whisper, source: "ru", translate: true, target: "en"));

    [Fact]
    public void Whisper_TranslationSwitchedOff_FollowsTheSource() =>
        Assert.Equal(
            CleanupLanguageKind.NotEnglish,
            Resolve(SpeechEngine.Whisper, source: "ru", translate: false, target: "en").Kind);

    [Fact]
    public void Whisper_TranslationPausedByTheModel_FollowsTheSource() =>
        // Large v3 Turbo cannot translate (ADR-061): the text stays Russian.
        Assert.Equal(
            CleanupLanguageKind.NotEnglish,
            Resolve(SpeechEngine.Whisper, source: "ru", translate: true, target: "en",
                model: WhisperModelType.LargeV3Turbo).Kind);

    [Theory]
    [InlineData(WhisperModelType.TinyEn)]
    [InlineData(WhisperModelType.BaseEn)]
    [InlineData(WhisperModelType.SmallEn)]
    [InlineData(WhisperModelType.MediumEn)]
    public void Whisper_EnglishOnlyModel_IsEnglish_WhateverIsStored(WhisperModelType model) =>
        Assert.Equal(CleanupLanguage.English, Resolve(SpeechEngine.Whisper, source: "ru", model: model));

    [Fact]
    public void Whisper_KeyboardSource_FollowsTheLayout()
    {
        Assert.Equal(
            CleanupLanguageKind.NotEnglish,
            Resolve(SpeechEngine.Whisper, LanguageCatalog.KeyboardLayoutCode, layout: RussianLayout).Kind);
        Assert.Equal(
            CleanupLanguage.English,
            Resolve(SpeechEngine.Whisper, LanguageCatalog.KeyboardLayoutCode, layout: EnglishLayout));
        Assert.Equal(
            CleanupLanguage.Unknown,
            Resolve(SpeechEngine.Whisper, LanguageCatalog.KeyboardLayoutCode, layout: null));
    }

    // --- Gemma 4 ---

    [Fact]
    public void Gemma_TranslatingToAnotherLanguage_IsTheTarget()
    {
        var language = Resolve(SpeechEngine.Gemma4, source: "en", translate: true, target: "de");

        Assert.Equal(CleanupLanguageKind.NotEnglish, language.Kind);
        Assert.Equal("de", language.Code);
    }

    [Fact]
    public void Gemma_TranslatingToEnglish_IsEnglish() =>
        Assert.Equal(
            CleanupLanguage.English,
            Resolve(SpeechEngine.Gemma4, source: LanguageCatalog.AutoDetectCode, translate: true, target: "en"));

    [Fact]
    public void Gemma_TargetSwitchedOff_FollowsTheSource()
    {
        Assert.Equal(
            CleanupLanguage.English,
            Resolve(SpeechEngine.Gemma4, source: "en", translate: true, target: LanguageCatalog.NoTranslationCode));
        Assert.Equal(
            CleanupLanguage.Unknown,
            Resolve(SpeechEngine.Gemma4, source: LanguageCatalog.AutoDetectCode, translate: false, target: "de"));
    }

    // --- Per-utterance refinement ---

    [Theory]
    [InlineData("en", CleanupLanguageKind.English)]
    [InlineData("EN", CleanupLanguageKind.English)]
    [InlineData("english", CleanupLanguageKind.English)]
    [InlineData("de", CleanupLanguageKind.NotEnglish)]
    [InlineData(null, CleanupLanguageKind.Unknown)]
    [InlineData("", CleanupLanguageKind.Unknown)]
    public void Refine_UnknownPlan_TakesTheRecognizersLanguage(string? detected, CleanupLanguageKind expected) =>
        Assert.Equal(expected, CleanupLanguagePolicy.Refine(CleanupLanguage.Unknown, detected).Kind);

    [Fact]
    public void Refine_KnownPlan_IsNotOverridden()
    {
        // Translating: Whisper reports the spoken language, the text is English.
        Assert.Equal(
            CleanupLanguage.English,
            CleanupLanguagePolicy.Refine(CleanupLanguage.English, "ru"));
    }

    // --- Gate: language known ---

    [Fact]
    public void Evaluate_KnownEnglish_TrustsTheSetting_AndDoesNotExamineTheText() =>
        // Would fail the text test: most of the words are accented names.
        Assert.Null(SpeechCleanupGate.Evaluate("Zoë, Chloë, Renée, hello", CleanupLanguage.English));

    [Fact]
    public void Evaluate_KnownOtherLanguage_SkipsEvenEnglishLookingText() =>
        Assert.Equal(
            SpeechCleanupOutcome.SkippedNotEnglish,
            SpeechCleanupGate.Evaluate("okay computer", CleanupLanguage.Of("it")));

    [Fact]
    public void Evaluate_TooLong_SaysSo() =>
        Assert.Equal(
            SpeechCleanupOutcome.SkippedTooLong,
            SpeechCleanupGate.Evaluate(
                new string('a', SpeechCleanupGate.MaxTranscriptChars + 1), CleanupLanguage.English));

    [Theory]
    [InlineData("")]
    [InlineData("12345 !!!")]
    public void Evaluate_NothingToClean_IsNotASkip(string text) =>
        Assert.Equal(SpeechCleanupOutcome.NotRun, SpeechCleanupGate.Evaluate(text, CleanupLanguage.Unknown));

    // --- Gate: language unknown, the text decides ---

    [Theory]
    [InlineData("meeting moved to Friday at three")]
    [InlineData("send it now")]
    [InlineData("okay thanks")]
    [InlineData("um yeah sure")]
    [InlineData("I met Mr. Müller at the café yesterday")]
    [InlineData("we went to Las Vegas with Sam and Ben")]
    [InlineData("the AI team will meet the EU delegation at the UN")]
    [InlineData("see you at the Tour de France")]
    [InlineData("so um i need to send the report by uh friday no wait thursday")]
    public void IsLikelyEnglish_AcceptsEnglish(string text) =>
        Assert.True(SpeechCleanupGate.IsLikelyEnglish(text));

    /// <summary>
    /// Sentences with no diacritics at all, which the script test alone let
    /// through and the model then damaged (ADR-073's known gap).
    /// </summary>
    [Theory]
    [InlineData("domani devo mandare il rapporto a Marco e poi andiamo a casa")]
    [InlineData("tengo que enviar el informe a Juan antes del viernes")]
    [InlineData("ik moet het rapport voor vrijdag naar Jan sturen")]
    [InlineData("jeg skal sende rapporten til Peter inden fredag")]
    [InlineData("jag ska skicka rapporten till Anna innan fredag")]
    [InlineData("ich muss den Bericht bis Freitag an Peter schicken")]
    [InlineData("je dois envoyer le rapport avant vendredi")]
    [InlineData("eu preciso enviar o relatorio para o Pedro antes de sexta")]
    [InlineData("to jest dobry pomysl i na pewno to zrobimy")]
    [InlineData("trebuie sa trimit raportul pentru Ana pana vineri")]
    [InlineData("saya harus mengirim laporan itu sebelum hari jumat")]
    [InlineData("bu raporu cuma gununden once gondermem gerekiyor")]
    [InlineData("moram poslati izvjestaj do petka i to je sve")]
    public void IsLikelyEnglish_RejectsOtherLatinLanguagesWithoutDiacritics(string text) =>
        Assert.False(SpeechCleanupGate.IsLikelyEnglish(text));

    [Theory]
    [InlineData("minun pitää lähettää raportti ennen perjantaita")]
    [InlineData("j'ai réservé une table pour ce soir à huit heures")]
    [InlineData("schöne Grüße")]
    public void IsLikelyEnglish_RejectsAccentedTextTheWordsDoNotVouchFor(string text) =>
        Assert.False(SpeechCleanupGate.IsLikelyEnglish(text));
}
