using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class LanguageSelectionSettingsViewModelTests
{
    private static async Task<LanguageSelectionSettingsViewModel> CreateAsync(
        MockSettingsService settings, SpeechEngine engine = SpeechEngine.Whisper)
    {
        await settings.SetAsync(SettingsKeys.SpeechEngine, engine.ToString(), TestContext.Current.CancellationToken);
        var vm = new LanguageSelectionSettingsViewModel(settings);
        await Task.Yield();
        return vm;
    }

    [Fact]
    public async Task Init_DefaultsToAutoSourceAndTranslationOff()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        Assert.Equal(LanguageCatalog.AutoDetectCode, vm.SelectedSourceCode);
        Assert.Equal(LanguageCatalog.NoTranslationCode, vm.SelectedTargetCode);
        Assert.False(vm.TranslationEnabled);
        Assert.False(vm.IsTargetButtonEnabled);
        Assert.Equal(LanguagePickerKind.None, vm.OpenPicker);
    }

    [Fact]
    public async Task Whisper_SourcePicker_StartsWithAutoSentinelThenWhisperLanguages()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        // Force the source picker to populate.
        vm.OpenSourcePickerCommand.Execute(null);

        Assert.Equal(LanguageCatalog.AutoDetectCode, vm.SourcePicker.Items[0].Code);
        Assert.Equal(LanguageCatalog.WhisperLanguages.Count + 1, vm.SourcePicker.Items.Count);
    }

    [Fact]
    public async Task Whisper_TargetPicker_OffersOnlyEnglish()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        // Translation must be on for the target picker to be opened.
        vm.ToggleTranslationCommand.Execute(null);
        vm.OpenTargetPickerCommand.Execute(null);

        // No leading sentinel for Whisper target; just English.
        Assert.Single(vm.TargetPicker.Items);
        Assert.Equal("en", vm.TargetPicker.Items[0].Code);
    }

    [Fact]
    public async Task Gemma4_TargetPicker_OffersFullCatalog()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings, SpeechEngine.Gemma4);

        vm.ToggleTranslationCommand.Execute(null);
        vm.OpenTargetPickerCommand.Execute(null);

        // Full list, with no leading sentinel (arrow handles enable/disable).
        Assert.Equal(LanguageCatalog.AllLanguages.Count, vm.TargetPicker.Items.Count);
    }

    [Fact]
    public async Task OpenSourcePicker_TogglesOpenState()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        vm.OpenSourcePickerCommand.Execute(null);
        Assert.Equal(LanguagePickerKind.Source, vm.OpenPicker);
        Assert.True(vm.IsSourcePickerOpen);

        vm.OpenSourcePickerCommand.Execute(null);
        Assert.Equal(LanguagePickerKind.None, vm.OpenPicker);
    }

    [Fact]
    public async Task OpenTargetPicker_IsNoOp_WhenTranslationDisabled()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        Assert.False(vm.TranslationEnabled);
        vm.OpenTargetPickerCommand.Execute(null);

        Assert.Equal(LanguagePickerKind.None, vm.OpenPicker);
    }

    [Fact]
    public async Task ToggleTranslation_OnWhisper_DefaultsTargetToEnglish()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        vm.ToggleTranslationCommand.Execute(null);
        await Task.Yield();

        Assert.True(vm.TranslationEnabled);
        Assert.Equal("en", vm.SelectedTargetCode);
        Assert.Equal(true.ToString(),
            await settings.GetAsync<string>(SettingsKeys.TranslationEnabled, TestContext.Current.CancellationToken));
        Assert.Equal("en",
            await settings.GetAsync<string>(SettingsKeys.SelectedTargetLanguage, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ToggleTranslation_OnGemma_DefaultsTargetToMostRecent()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.RecentTargetLanguages, new List<string> { "fr", "de" }, TestContext.Current.CancellationToken);
        var vm = await CreateAsync(settings, SpeechEngine.Gemma4);

        vm.ToggleTranslationCommand.Execute(null);
        await Task.Yield();

        Assert.True(vm.TranslationEnabled);
        Assert.Equal("fr", vm.SelectedTargetCode);
    }

    [Fact]
    public async Task ToggleTranslation_Off_ClosesTargetPickerIfOpen()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        vm.ToggleTranslationCommand.Execute(null);   // on
        vm.OpenTargetPickerCommand.Execute(null);    // opens target picker
        Assert.Equal(LanguagePickerKind.Target, vm.OpenPicker);

        vm.ToggleTranslationCommand.Execute(null);   // off — should collapse the picker
        Assert.Equal(LanguagePickerKind.None, vm.OpenPicker);
    }

    [Fact]
    public async Task ToggleTranslation_PreservesTargetAcrossToggle()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings, SpeechEngine.Gemma4);

        vm.ToggleTranslationCommand.Execute(null);   // on, default target
        vm.SelectTarget_TestHook("ru");
        await Task.Yield();
        Assert.Equal("ru", vm.SelectedTargetCode);

        vm.ToggleTranslationCommand.Execute(null);   // off — target preserved
        Assert.False(vm.TranslationEnabled);
        Assert.Equal("ru", vm.SelectedTargetCode);

        vm.ToggleTranslationCommand.Execute(null);   // on again — target unchanged
        Assert.Equal("ru", vm.SelectedTargetCode);
    }

    [Fact]
    public async Task SelectSource_UpdatesSelectionPersistsAndPromotesSourceMru()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        vm.OpenSourcePickerCommand.Execute(null);
        vm.SourcePicker.SelectCommand.Execute("ru");
        await Task.Yield();

        Assert.Equal("ru", vm.SelectedSourceCode);
        Assert.Equal("ru",
            await settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, TestContext.Current.CancellationToken));

        var sourceMru = await settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages, TestContext.Current.CancellationToken);
        Assert.Equal(["ru"], sourceMru);

        // Target MRU untouched.
        Assert.Null(await settings.GetAsync<List<string>>(SettingsKeys.RecentTargetLanguages, TestContext.Current.CancellationToken));

        // Picker closes after selection.
        Assert.Equal(LanguagePickerKind.None, vm.OpenPicker);
    }

    [Fact]
    public async Task SelectTarget_OnGemma_PromotesTargetMruOnly()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings, SpeechEngine.Gemma4);

        vm.ToggleTranslationCommand.Execute(null);
        vm.OpenTargetPickerCommand.Execute(null);
        vm.TargetPicker.SelectCommand.Execute("fr");
        await Task.Yield();

        Assert.Equal("fr", vm.SelectedTargetCode);

        var targetMru = await settings.GetAsync<List<string>>(SettingsKeys.RecentTargetLanguages, TestContext.Current.CancellationToken);
        Assert.Equal(["fr"], targetMru);

        // Source MRU untouched.
        Assert.Null(await settings.GetAsync<List<string>>(SettingsKeys.RecentSourceLanguages, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task SourcePicker_Filter_NarrowsList()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        vm.OpenSourcePickerCommand.Execute(null);
        vm.SourcePicker.Filter = "russ";

        // The auto-detect sentinel disappears while filtering.
        Assert.DoesNotContain(vm.SourcePicker.Items, i => i.Code == LanguageCatalog.AutoDetectCode);
        Assert.Contains(vm.SourcePicker.Items, i => i.Code == "ru");
        Assert.All(vm.SourcePicker.Items, i =>
            Assert.Contains("russ", i.DisplayName, StringComparison.OrdinalIgnoreCase));
        Assert.False(vm.SourcePicker.HasNoResults);
    }

    [Fact]
    public async Task UpdateForEngine_SwitchToGemma_AllowsArbitraryTarget()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);
        vm.ToggleTranslationCommand.Execute(null);
        vm.OpenTargetPickerCommand.Execute(null);

        // Whisper target list: English only.
        Assert.Single(vm.TargetPicker.Items);

        vm.UpdateForEngine(SpeechEngine.Gemma4);

        // After engine switch the target picker now exposes the full catalog.
        Assert.Equal(LanguageCatalog.AllLanguages.Count, vm.TargetPicker.Items.Count);
    }

    [Fact]
    public async Task UpdateTranslationAvailability_FlipsPausedNote_WhenWhisperModelCannotTranslate()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);
        vm.ToggleTranslationCommand.Execute(null);

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);
        Assert.True(vm.ShowTranslationPausedNote);

        vm.UpdateTranslationAvailability(WhisperModelType.Medium);
        Assert.False(vm.ShowTranslationPausedNote);
    }

    [Fact]
    public async Task ShowTranslationPausedNote_IsFalse_OnGemma4_Regardless()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings, SpeechEngine.Gemma4);
        vm.ToggleTranslationCommand.Execute(null);

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);

        Assert.False(vm.ShowTranslationPausedNote);
    }

    [Fact]
    public async Task Init_MigratesLegacyTranslateToEnglish_TurnsTranslationOn()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.TranslateToEnglish, true.ToString(), TestContext.Current.CancellationToken);

        var vm = await CreateAsync(settings);

        Assert.True(vm.TranslationEnabled);
        Assert.Equal("en", vm.SelectedTargetCode);
    }

    [Fact]
    public async Task Init_MigratesLegacyRecentLanguages_IntoSourceMru()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.RecentLanguages,
            new List<string> { "fr", "ru" }, TestContext.Current.CancellationToken);

        var vm = await CreateAsync(settings);

        vm.OpenSourcePickerCommand.Execute(null);

        // Auto-detect (index 0), then the migrated recents in order.
        Assert.Equal(LanguageCatalog.AutoDetectCode, vm.SourcePicker.Items[0].Code);
        Assert.Equal("fr", vm.SourcePicker.Items[1].Code);
        Assert.True(vm.SourcePicker.Items[1].IsRecent);
        Assert.Equal("ru", vm.SourcePicker.Items[2].Code);
        Assert.True(vm.SourcePicker.Items[2].IsRecent);
    }
}

/// <summary>
/// Test hooks for invoking private select-callbacks. The callbacks are intentionally
/// private (they're plumbed through the child picker VMs), but tests need direct
/// access to seed state without going through the full command pipeline.
/// </summary>
internal static class LanguageSelectionSettingsViewModelTestExtensions
{
    public static void SelectTarget_TestHook(this LanguageSelectionSettingsViewModel vm, string code)
    {
        vm.OpenTargetPickerCommand.Execute(null);
        vm.TargetPicker.SelectCommand.Execute(code);
    }
}
