using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public class LlamaCppPromptBuildingTests
{
    private static LlamaCppSpeechRecognizer CreateRecognizer(ISettingsService settings) =>
        // registry/prompts are unused by BuildPromptTextAsync.
        new(settings, registry: null!, prompts: null!, NullLogger<LlamaCppSpeechRecognizer>.Instance);

    private static readonly PromptTemplate Prompt =
        new("p1", "Test", "Transcribe the {language} audio.");

    [Fact]
    public async Task NoLanguageSettings_RendersDefaultLanguage_NoTranslation()
    {
        var vm = CreateRecognizer(new FakeSettings());

        var text = await vm.BuildPromptTextAsync(Prompt, CancellationToken.None);

        Assert.Equal("Transcribe the English audio.", text);
    }

    [Fact]
    public async Task ExplicitSource_RendersSourceLanguageName()
    {
        var settings = new FakeSettings();
        settings.Values[SettingsKeys.SelectedSourceLanguage] = "ru";

        var vm = CreateRecognizer(settings);
        var text = await vm.BuildPromptTextAsync(Prompt, CancellationToken.None);

        Assert.Equal("Transcribe the Russian audio.", text);
    }

    [Fact]
    public async Task AutoSource_FallsBackToDefaultLanguage()
    {
        var settings = new FakeSettings();
        settings.Values[SettingsKeys.SelectedSourceLanguage] = LanguageCatalog.AutoDetectCode;

        var vm = CreateRecognizer(settings);
        var text = await vm.BuildPromptTextAsync(Prompt, CancellationToken.None);

        Assert.Equal("Transcribe the English audio.", text);
    }

    [Fact]
    public async Task TargetLanguage_AppendsTranslationInstruction()
    {
        var settings = new FakeSettings();
        settings.Values[SettingsKeys.SelectedSourceLanguage] = "ru";
        settings.Values[SettingsKeys.SelectedTargetLanguage] = "fr";

        var vm = CreateRecognizer(settings);
        var text = await vm.BuildPromptTextAsync(Prompt, CancellationToken.None);

        Assert.StartsWith("Transcribe the Russian audio.", text);
        Assert.Contains("translate the transcript into French", text);
    }

    [Fact]
    public async Task NoneTarget_DoesNotAppendTranslation()
    {
        var settings = new FakeSettings();
        settings.Values[SettingsKeys.SelectedTargetLanguage] = LanguageCatalog.NoTranslationCode;

        var vm = CreateRecognizer(settings);
        var text = await vm.BuildPromptTextAsync(Prompt, CancellationToken.None);

        Assert.DoesNotContain("translate", text, StringComparison.OrdinalIgnoreCase);
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
