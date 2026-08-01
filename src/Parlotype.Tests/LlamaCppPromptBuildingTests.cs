using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class LlamaCppPromptBuildingTests
{
    private static LlamaCppSpeechRecognizer CreateRecognizer(
        ISettingsService settings, KeyboardLayoutInfo? keyboardLayout = null) =>
        // registry/prompts are unused by BuildPromptTextAsync.
        new(settings, registry: null!, prompts: null!,
            new FakeKeyboardLayout(keyboardLayout),
            NullLogger<LlamaCppSpeechRecognizer>.Instance);

    private sealed class FakeKeyboardLayout(KeyboardLayoutInfo? result) : IKeyboardLayoutService
    {
        public KeyboardLayoutInfo? Detect() => result;
    }

    // Custom (single-body) prompt — no translation/auto-detect bodies.
    private static readonly PromptTemplate CustomPrompt =
        new("p1", "Test", "Transcribe the {speech_lang} audio.");

    // Built-in default carries all three bodies.
    private static readonly PromptTemplate BuiltInPrompt =
        new("builtin", "Default", "Transcribe in {speech_lang}.",
            IsBuiltIn: true,
            TranslationText: "Transcribe in {speech_lang} and translate into {text_lang}.",
            AutoDetectText: "Detect the language and transcribe.");

    private static FakeSettings Settings(string? source = null, string? target = null, bool? translation = null)
    {
        var s = new FakeSettings();
        if (source is not null) s.Values[SettingsKeys.SelectedSourceLanguage] = source;
        if (target is not null) s.Values[SettingsKeys.SelectedTargetLanguage] = target;
        if (translation is not null) s.Values[SettingsKeys.TranslationEnabled] = translation.Value.ToString();
        return s;
    }

    [Fact]
    public async Task NoLanguageSettings_RendersAutoDetectedLanguage_NoTranslation()
    {
        var vm = CreateRecognizer(new FakeSettings());

        var text = await vm.BuildPromptTextAsync(CustomPrompt, CancellationToken.None);

        Assert.Equal("Transcribe the the detected language audio.", text);
    }

    [Fact]
    public async Task ExplicitSource_RendersSourceLanguageName()
    {
        var vm = CreateRecognizer(Settings(source: "ru"));

        var text = await vm.BuildPromptTextAsync(CustomPrompt, CancellationToken.None);

        Assert.Equal("Transcribe the Russian audio.", text);
    }

    [Fact]
    public async Task KeyboardSource_RendersDetectedLayoutLanguage()
    {
        var vm = CreateRecognizer(
            Settings(source: LanguageCatalog.KeyboardLayoutCode),
            new KeyboardLayoutInfo("ru", "Russian (Russia)"));

        var text = await vm.BuildPromptTextAsync(CustomPrompt, CancellationToken.None);

        Assert.Equal("Transcribe the Russian audio.", text);
    }

    [Fact]
    public async Task KeyboardSource_DetectionUnavailable_RendersAutoDetectedLanguage()
    {
        var vm = CreateRecognizer(
            Settings(source: LanguageCatalog.KeyboardLayoutCode), keyboardLayout: null);

        var text = await vm.BuildPromptTextAsync(CustomPrompt, CancellationToken.None);

        Assert.Equal("Transcribe the the detected language audio.", text);
    }

    [Fact]
    public async Task CustomPrompt_AppendsTranslationInstruction_WhenToggleOnAndTargetDiffers()
    {
        var vm = CreateRecognizer(Settings("ru", "fr", translation: true));

        var text = await vm.BuildPromptTextAsync(CustomPrompt, CancellationToken.None);

        Assert.StartsWith("Transcribe the Russian audio.", text);
        Assert.Contains("translate the transcript into French", text);
    }

    [Fact]
    public async Task CustomPrompt_SkipsTranslation_WhenToggleOff()
    {
        var vm = CreateRecognizer(Settings("ru", "fr", translation: false));

        var text = await vm.BuildPromptTextAsync(CustomPrompt, CancellationToken.None);

        Assert.DoesNotContain("translate", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task NoneTarget_DoesNotAppendTranslation()
    {
        var vm = CreateRecognizer(
            Settings("ru", LanguageCatalog.NoTranslationCode, translation: true));

        var text = await vm.BuildPromptTextAsync(CustomPrompt, CancellationToken.None);

        Assert.DoesNotContain("translate", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SameSourceAndTarget_DoesNotTranslate()
    {
        var vm = CreateRecognizer(Settings("en", "en", translation: true));

        var text = await vm.BuildPromptTextAsync(CustomPrompt, CancellationToken.None);

        Assert.DoesNotContain("translate", text, StringComparison.OrdinalIgnoreCase);
    }

    // A custom prompt using {text_lang} must never leak the raw token, on any path.
    [Fact]
    public async Task CustomPrompt_WithTextLangToken_Translating_RendersTargetLanguage()
    {
        var prompt = new PromptTemplate("p2", "Test", "Transcribe {speech_lang} into {text_lang} text.");
        var vm = CreateRecognizer(Settings("ru", "fr", translation: true));

        var text = await vm.BuildPromptTextAsync(prompt, CancellationToken.None);

        Assert.StartsWith("Transcribe Russian into French text.", text);
        Assert.Contains("translate the transcript into French", text);
        Assert.DoesNotContain(PromptTemplate.TextLanguageToken, text, StringComparison.Ordinal);
    }

    // Without translation the output language *is* the spoken language, so a custom
    // prompt using {text_lang} must render it rather than leak the raw token.
    [Fact]
    public async Task CustomPrompt_WithTextLangToken_NoTranslation_RendersSpeechLanguage()
    {
        var prompt = new PromptTemplate("p2", "Test", "Transcribe {speech_lang} into {text_lang} text.");
        var vm = CreateRecognizer(Settings("ru", "fr", translation: false));

        var text = await vm.BuildPromptTextAsync(prompt, CancellationToken.None);

        Assert.Equal("Transcribe Russian into Russian text.", text);
    }

    [Fact]
    public async Task CustomPrompt_WithTextLangToken_AutoSource_NoTranslation_RendersDetectedLanguage()
    {
        var prompt = new PromptTemplate("p2", "Test", "Transcribe {speech_lang} into {text_lang} text.");
        var vm = CreateRecognizer(Settings(source: LanguageCatalog.AutoDetectCode));

        var text = await vm.BuildPromptTextAsync(prompt, CancellationToken.None);

        Assert.Equal("Transcribe the detected language into the detected language text.", text);
    }

    [Fact]
    public async Task BuiltIn_KnownSource_NoTarget_UsesTranscriptionBody()
    {
        var vm = CreateRecognizer(Settings(source: "ru"));

        var text = await vm.BuildPromptTextAsync(BuiltInPrompt, CancellationToken.None);

        Assert.Equal("Transcribe in Russian.", text);
    }

    [Fact]
    public async Task BuiltIn_KnownSource_Target_ToggleOn_UsesTranslationBody()
    {
        var vm = CreateRecognizer(Settings("ru", "en", translation: true));

        var text = await vm.BuildPromptTextAsync(BuiltInPrompt, CancellationToken.None);

        Assert.Equal("Transcribe in Russian and translate into English.", text);
    }

    [Fact]
    public async Task BuiltIn_KnownSource_Target_ToggleOff_UsesTranscriptionBody()
    {
        var vm = CreateRecognizer(Settings("ru", "en", translation: false));

        var text = await vm.BuildPromptTextAsync(BuiltInPrompt, CancellationToken.None);

        Assert.Equal("Transcribe in Russian.", text);
    }

    [Fact]
    public async Task BuiltIn_AutoSource_NoTarget_UsesAutoDetectBody()
    {
        var vm = CreateRecognizer(Settings(source: LanguageCatalog.AutoDetectCode));

        var text = await vm.BuildPromptTextAsync(BuiltInPrompt, CancellationToken.None);

        Assert.Equal("Detect the language and transcribe.", text);
    }

    [Fact]
    public async Task BuiltIn_AutoSource_Target_ToggleOn_UsesTranslationBody_WithDetectedLanguage()
    {
        var vm = CreateRecognizer(
            Settings(LanguageCatalog.AutoDetectCode, "fr", translation: true));

        var text = await vm.BuildPromptTextAsync(BuiltInPrompt, CancellationToken.None);

        Assert.Equal("Transcribe in the detected language and translate into French.", text);
    }

    [Fact]
    public async Task BuiltIn_AutoSource_Target_ToggleOff_UsesAutoDetectBody()
    {
        var vm = CreateRecognizer(
            Settings(LanguageCatalog.AutoDetectCode, "fr", translation: false));

        var text = await vm.BuildPromptTextAsync(BuiltInPrompt, CancellationToken.None);

        Assert.Equal("Detect the language and transcribe.", text);
    }

    private sealed class FakeSettings : ISettingsService
    {
        public Dictionary<string, object?> Values { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult(Values.TryGetValue(key, out var v) && v is T t ? t : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            Values[key] = value;
            return Task.CompletedTask;
        }
    }
}
