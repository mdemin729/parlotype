using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class LanguageSelectionSettingsViewModelTests
{
    [Fact]
    public async Task Whisper_ShowsSourceFromWhisperSet_AndHidesTargetPicker()
    {
        var settings = new MockSettingsService();
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        Assert.False(vm.ShowTargetPicker);
        Assert.True(vm.ShowWhisperTranslationHint);
        Assert.Empty(vm.TargetLanguages);

        // Auto-detect is first, then the Whisper languages.
        Assert.Equal(LanguageCatalog.AutoDetectCode, vm.SourceLanguages[0].Code);
        Assert.Equal(LanguageCatalog.WhisperLanguages.Count + 1, vm.SourceLanguages.Count);
    }

    [Fact]
    public async Task Gemma4_ShowsTargetPicker_WithFullList()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);

        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        Assert.True(vm.ShowTargetPicker);
        Assert.False(vm.ShowWhisperTranslationHint);
        Assert.Equal(LanguageCatalog.NoTranslationCode, vm.TargetLanguages[0].Code);
        Assert.Equal(LanguageCatalog.AllLanguages.Count + 1, vm.TargetLanguages.Count);
    }

    [Fact]
    public async Task SelectSource_UpdatesSelectionAndPersists()
    {
        var settings = new MockSettingsService();
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        vm.SelectSourceCommand.Execute("ru");
        await Task.Yield();

        Assert.Equal("ru", vm.SelectedSourceCode);
        Assert.Equal("ru", await settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, TestContext.Current.CancellationToken));
        Assert.True(vm.SourceLanguages.First(i => i.Code == "ru").IsSelected);
    }

    [Fact]
    public async Task SelectSource_PinsToRecent_OnTopAfterAuto()
    {
        var settings = new MockSettingsService();
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        vm.SelectSourceCommand.Execute("ru");
        await Task.Yield();

        // Index 0 is Auto-detect; the just-selected language is pinned right after.
        Assert.Equal(LanguageCatalog.AutoDetectCode, vm.SourceLanguages[0].Code);
        Assert.Equal("ru", vm.SourceLanguages[1].Code);
        Assert.True(vm.SourceLanguages[1].IsRecent);

        var recent = await settings.GetAsync<List<string>>(SettingsKeys.RecentLanguages, TestContext.Current.CancellationToken);
        Assert.Equal(["ru"], recent);
    }

    [Fact]
    public async Task SelectTarget_OnGemma_PersistsAndPins()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);

        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        vm.SelectTargetCommand.Execute("fr");
        await Task.Yield();

        Assert.Equal("fr", vm.SelectedTargetCode);
        Assert.Equal("fr", await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage, TestContext.Current.CancellationToken));
        // Default(none) first, then the pinned recent.
        Assert.Equal(LanguageCatalog.NoTranslationCode, vm.TargetLanguages[0].Code);
        Assert.Equal("fr", vm.TargetLanguages[1].Code);
    }

    [Fact]
    public async Task RecentList_IsSharedAcrossPickers_TargetSelectionUpdatesSource()
    {
        // On Gemma 4 both pickers are visible and draw from the full language list, and
        // the recently-used list is shared between them. Selecting a target language must
        // therefore immediately surface that language as a pinned recent in the source
        // picker too (regression: previously only the originating picker was rebuilt).
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);

        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        vm.SelectTargetCommand.Execute("fr");
        await Task.Yield();

        // Source list: index 0 is Auto-detect, the shared recent is pinned right after.
        Assert.Equal("fr", vm.SourceLanguages[1].Code);
        Assert.True(vm.SourceLanguages[1].IsRecent);

        // And a source selection propagates back into the target picker's recents.
        vm.SelectSourceCommand.Execute("de");
        await Task.Yield();

        // Target list: index 0 is Default(none), then the most-recent pinned languages.
        Assert.Equal("de", vm.TargetLanguages[1].Code);
        Assert.True(vm.TargetLanguages[1].IsRecent);
        Assert.Equal("fr", vm.TargetLanguages[2].Code);
    }

    [Fact]
    public async Task SourceFilter_NarrowsListAndHidesAutoSentinel()
    {
        var settings = new MockSettingsService();
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        vm.SourceFilter = "russ";

        Assert.DoesNotContain(vm.SourceLanguages, i => i.Code == LanguageCatalog.AutoDetectCode);
        Assert.Contains(vm.SourceLanguages, i => i.Code == "ru");
        Assert.All(vm.SourceLanguages, i =>
            Assert.Contains("russ", i.DisplayName, StringComparison.OrdinalIgnoreCase));
        Assert.False(vm.SourceHasNoResults);
    }

    [Fact]
    public async Task SourceFilter_MatchesNativeName()
    {
        var settings = new MockSettingsService();
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        vm.SourceFilter = "рус"; // Russian endonym

        Assert.Contains(vm.SourceLanguages, i => i.Code == "ru");
    }

    [Fact]
    public async Task SourceFilter_Cleared_RestoresFullListWithAutoSentinel()
    {
        var settings = new MockSettingsService();
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();
        var fullCount = vm.SourceLanguages.Count;

        vm.SourceFilter = "russ";
        Assert.True(vm.SourceLanguages.Count < fullCount);

        vm.SourceFilter = "";

        Assert.Equal(fullCount, vm.SourceLanguages.Count);
        Assert.Equal(LanguageCatalog.AutoDetectCode, vm.SourceLanguages[0].Code);
        Assert.False(vm.SourceHasNoResults);
    }

    [Fact]
    public async Task SourceFilter_NoMatch_SetsNoResults()
    {
        var settings = new MockSettingsService();
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        vm.SourceFilter = "zzzzz";

        Assert.Empty(vm.SourceLanguages);
        Assert.True(vm.SourceHasNoResults);
    }

    [Fact]
    public async Task TargetFilter_FiltersIndependentlyOfSource_AndHidesNoneSentinel()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);

        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();

        vm.TargetFilter = "french";

        // Target filtered down; the "none" sentinel hidden while searching.
        Assert.DoesNotContain(vm.TargetLanguages, i => i.Code == LanguageCatalog.NoTranslationCode);
        Assert.Contains(vm.TargetLanguages, i => i.Code == "fr");
        // Source picker untouched by the target filter.
        Assert.Equal(LanguageCatalog.AutoDetectCode, vm.SourceLanguages[0].Code);
    }

    [Fact]
    public async Task UpdateForEngine_SwitchToGemma_RevealsTargetPicker()
    {
        var settings = new MockSettingsService();
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();
        Assert.False(vm.ShowTargetPicker);

        vm.UpdateForEngine(SpeechEngine.Gemma4);

        Assert.True(vm.ShowTargetPicker);
        Assert.NotEmpty(vm.TargetLanguages);
    }
}
