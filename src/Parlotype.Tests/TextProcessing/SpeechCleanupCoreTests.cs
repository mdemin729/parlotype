using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;
using Parlotype.Core.TextProcessing;
using Xunit;

namespace Parlotype.Tests.TextProcessing;

/// <summary>
/// The pure parts of speech cleanup (ADR-073): the S1-mini input format, the
/// gate that decides what is sent and what is accepted back, and the settings
/// defaults.
/// </summary>
public sealed class SpeechCleanupCoreTests
{
    private sealed class InMemorySettings : ISettingsService
    {
        private readonly Dictionary<string, object?> _store = new(StringComparer.Ordinal);

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(_store.TryGetValue(key, out var raw) && raw is T typed ? typed : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }

    // --- S1-mini input format ---

    [Fact]
    public void SystemPrompt_IsTheModelCardsWordingVerbatim()
    {
        // Copied from the model card independently of the constant. A reworded
        // prompt makes the model hallucinate, so an edit to either must fail here.
        const string published =
            "You are a text normalizer for speech-to-text transcripts. The input begins with a control line " +
            "specifying the styling, structure, and context settings; clean the transcript to match those " +
            "settings and output only the cleaned text.";

        Assert.Equal(published, S1MiniPrompt.SystemPrompt);
    }

    [Fact]
    public void BuildUserMessage_DefaultOptions_MatchesTheModelCardExample()
    {
        var message = S1MiniPrompt.BuildUserMessage(
            SpeechCleanupOptions.Default,
            "so um i need to like send the the report by uh friday no wait make that thursday");

        Assert.Equal(
            "[Styling: semi-formal] [Structure: prose] [Context: general]\n" +
            "so um i need to like send the the report by uh friday no wait make that thursday",
            message);
    }

    [Theory]
    [InlineData(CleanupStyling.Casual, "casual")]
    [InlineData(CleanupStyling.SemiCasual, "semi-casual")]
    [InlineData(CleanupStyling.SemiFormal, "semi-formal")]
    [InlineData(CleanupStyling.Formal, "formal")]
    public void BuildUserMessage_WritesEachStylingAsItsTrainedValue(CleanupStyling styling, string expected)
    {
        var message = S1MiniPrompt.BuildUserMessage(new SpeechCleanupOptions(Styling: styling), "x");

        Assert.StartsWith($"[Styling: {expected}] ", message);
    }

    [Fact]
    public void BuildUserMessage_WritesListsAndEmail()
    {
        var message = S1MiniPrompt.BuildUserMessage(
            new SpeechCleanupOptions(CleanupStyling.Formal, CleanupStructure.Lists, CleanupContext.Email),
            "hello");

        Assert.Equal("[Styling: formal] [Structure: lists] [Context: email]\nhello", message);
    }

    [Fact]
    public void BuildUserMessage_RejectsAValueOutsideTheTrainedSet()
    {
        var options = new SpeechCleanupOptions(Styling: (CleanupStyling)42);

        Assert.Throws<ArgumentOutOfRangeException>(() => S1MiniPrompt.BuildUserMessage(options, "x"));
    }

    // --- Gate: what is sent ---

    [Theory]
    [InlineData("so um i need to send the report by friday")]
    [InlineData("Call me at 5 p.m. — it's urgent!")]
    [InlineData("I ordered a café latte for the two of us at the hotel bar this morning")]
    public void ShouldClean_AcceptsEnglish(string text) =>
        Assert.True(SpeechCleanupGate.ShouldClean(text));

    [Theory]
    [InlineData("мне нужно отправить отчёт до пятницы")]
    [InlineData("Πρέπει να στείλω την αναφορά")]
    [InlineData("Ich möchte für nächste Woche fünf Bücher über Vögel")]
    [InlineData("Zażółć gęślą jaźń")]
    public void ShouldClean_RejectsOtherLanguages(string text) =>
        Assert.False(SpeechCleanupGate.ShouldClean(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("12345 !!!")]
    public void ShouldClean_RejectsTextWithNothingToClean(string? text) =>
        Assert.False(SpeechCleanupGate.ShouldClean(text));

    [Fact]
    public void ShouldClean_RejectsATranscriptTooLongForTheContext()
    {
        var atLimit = new string('a', SpeechCleanupGate.MaxTranscriptChars);

        Assert.True(SpeechCleanupGate.ShouldClean(atLimit));
        Assert.False(SpeechCleanupGate.ShouldClean(atLimit + "a"));
    }

    // --- Gate: what is accepted back ---

    [Fact]
    public void IsPlausibleResult_AcceptsAShorterRewrite() =>
        Assert.True(SpeechCleanupGate.IsPlausibleResult(
            "so um i need to like send the the report by uh friday no wait make that thursday",
            "I need to send the report by Thursday."));

    [Fact]
    public void IsPlausibleResult_AcceptsEmpty_BecauseFillerCleansToNothing() =>
        Assert.True(SpeechCleanupGate.IsPlausibleResult("um uh hmm", string.Empty));

    [Fact]
    public void IsPlausibleResult_AcceptsAListLayoutOfAShortInput() =>
        Assert.True(SpeechCleanupGate.IsPlausibleResult(
            "we need milk eggs and bread",
            "We need:\n- Milk\n- Eggs\n- Bread"));

    [Fact]
    public void IsPlausibleResult_RejectsOutputThatOutgrowsItsInput()
    {
        var raw = "please send the report";
        var looping = string.Concat(Enumerable.Repeat("Please send the report. ", 10));

        Assert.False(SpeechCleanupGate.IsPlausibleResult(raw, looping));
    }

    // --- Settings ---

    [Fact]
    public async Task Settings_Unset_IsOffWithDefaultOptions()
    {
        var loaded = await SpeechCleanupSettings.LoadAsync(new InMemorySettings());

        Assert.False(loaded.Enabled);
        Assert.Equal(SpeechCleanupOptions.Default, loaded.Options);
        Assert.Equal(CleanupStyling.SemiFormal, loaded.Options.Styling);
    }

    [Fact]
    public async Task Settings_ReadsWhatWasSaved()
    {
        var settings = new InMemorySettings();
        await settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, "True");
        await settings.SetAsync(SettingsKeys.SpeechCleanupStyling, "Formal");
        await settings.SetAsync(SettingsKeys.SpeechCleanupStructure, "Lists");
        await settings.SetAsync(SettingsKeys.SpeechCleanupContext, "Email");

        var loaded = await SpeechCleanupSettings.LoadAsync(settings);

        Assert.True(loaded.Enabled);
        Assert.Equal(
            new SpeechCleanupOptions(CleanupStyling.Formal, CleanupStructure.Lists, CleanupContext.Email),
            loaded.Options);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("42")]
    public async Task Settings_UnusableValue_FallsBackToTheDefault(string stored)
    {
        var settings = new InMemorySettings();
        await settings.SetAsync(SettingsKeys.SpeechCleanupStyling, stored);

        var loaded = await SpeechCleanupSettings.LoadAsync(settings);

        Assert.Equal(CleanupStyling.SemiFormal, loaded.Options.Styling);
    }

    // --- Model catalog ---

    [Fact]
    public void S1Mini_KeepsTheNameItsLicenceRequires() =>
        Assert.Equal("S1-mini by Superwhisper", SpeechCleanupModelInfo.S1Mini.DisplayName);

    [Fact]
    public void EveryCatalogModel_CarriesAWellFormedSha256()
    {
        foreach (var model in SpeechCleanupModelInfo.All)
        {
            Assert.Equal(64, model.GgufSha256.Length);
            Assert.All(model.GgufSha256, c => Assert.True(char.IsAsciiHexDigitLower(c)));
            Assert.EndsWith(".gguf", model.GgufFileName);
        }
    }

    // --- Server build choice ---

    private static LlamaServerVariant Variant(string build, LlamaServerBackend backend) => new(
        Build: build,
        Backend: backend,
        Os: LlamaServerOs.Windows,
        Arch: LlamaServerArch.X64,
        AssetName: $"llama-{build}-bin-win-{backend}-x64.zip",
        Bytes: 1,
        DownloadUrl: "https://example.invalid/asset.zip",
        Sha256: null,
        CompanionAssetName: null,
        CompanionDownloadUrl: null,
        CompanionBytes: null,
        CompanionSha256: null);

    [Fact]
    public void PickCpuVariant_TakesTheCpuBuildOfTheNewestRelease()
    {
        IReadOnlyList<LlamaServerReleaseGroup> groups =
        [
            new("b200", [Variant("b200", LlamaServerBackend.Cuda13), Variant("b200", LlamaServerBackend.Cpu)]),
            new("b100", [Variant("b100", LlamaServerBackend.Cpu)]),
        ];

        var picked = LlamaServerVariantPicker.PickCpuVariant(groups);

        Assert.NotNull(picked);
        Assert.Equal("b200", picked.Build);
        Assert.Equal(LlamaServerBackend.Cpu, picked.Backend);
    }

    [Fact]
    public void PickCpuVariant_FallsBackToAnOlderRelease_WhenTheNewestHasNoCpuBuild()
    {
        IReadOnlyList<LlamaServerReleaseGroup> groups =
        [
            new("b200", [Variant("b200", LlamaServerBackend.Vulkan)]),
            new("b100", [Variant("b100", LlamaServerBackend.Cpu)]),
        ];

        Assert.Equal("b100", LlamaServerVariantPicker.PickCpuVariant(groups)?.Build);
    }

    [Fact]
    public void PickCpuVariant_ReturnsNull_WhenNoReleaseHasACpuBuild()
    {
        IReadOnlyList<LlamaServerReleaseGroup> groups =
            [new("b200", [Variant("b200", LlamaServerBackend.Vulkan)])];

        Assert.Null(LlamaServerVariantPicker.PickCpuVariant(groups));
        Assert.Null(LlamaServerVariantPicker.PickCpuVariant([]));
    }
}
