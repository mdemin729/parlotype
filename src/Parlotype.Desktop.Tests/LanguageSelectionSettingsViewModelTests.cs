using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

public class LanguageSelectionSettingsViewModelTests
{
    private const SpeechEngine TranscribeOnlyEngine = (SpeechEngine)999;

    private static async Task<LanguageSelectionSettingsViewModel> CreateAsync(
        MockSettingsService settings,
        SpeechEngine engine = SpeechEngine.Whisper,
        KeyboardLayoutInfo? layout = null)
    {
        await settings.SetAsync(SettingsKeys.SpeechEngine, engine.ToString(), TestContext.Current.CancellationToken);
        var relationship = new LanguageRelationshipViewModel(
            settings, new MockKeyboardLayoutService { Result = layout });
        await relationship.InitializeAsync(TestContext.Current.CancellationToken);
        return new LanguageSelectionSettingsViewModel(relationship);
    }

    // ----- Forms per engine (spec §4 / FR-T1..T4) -------------------------------

    [Fact]
    public async Task Whisper_RendersToggleForm()
    {
        var vm = await CreateAsync(new MockSettingsService());

        Assert.True(vm.IsToggleForm);
        Assert.False(vm.IsFullForm);
        Assert.False(vm.IsNoneForm);
        Assert.Equal("Translate to English", vm.ToggleSwitchLabel);
    }

    [Fact]
    public async Task Gemma4_RendersFullForm()
    {
        var vm = await CreateAsync(new MockSettingsService(), SpeechEngine.Gemma4);

        Assert.True(vm.IsFullForm);
        Assert.False(vm.IsToggleForm);
        Assert.False(vm.IsNoneForm);
    }

    [Fact]
    public async Task TranscribeOnlyEngine_RendersNoneForm_LockedConnector()
    {
        var vm = await CreateAsync(new MockSettingsService());

        vm.UpdateForEngine(TranscribeOnlyEngine);

        Assert.True(vm.IsNoneForm);
        Assert.True(vm.IsConnectorLocked);
        Assert.False(vm.IsConnectorOn);
        Assert.Contains("can't translate", vm.UnavailableNote);
    }

    [Fact]
    public async Task EngineSwitch_MorphsFormInPlace()
    {
        var vm = await CreateAsync(new MockSettingsService());
        Assert.True(vm.IsToggleForm);

        vm.UpdateForEngine(SpeechEngine.Gemma4);

        Assert.True(vm.IsFullForm);
        Assert.False(vm.IsToggleForm);
    }

    // ----- Connector / switch (FR-C1..C3) ----------------------------------------

    [Fact]
    public async Task Connector_TogglesTranslation_OneAction()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);
        Assert.True(vm.IsConnectorOff);

        vm.ToggleTranslationCommand.Execute(null);

        Assert.True(vm.IsConnectorOn);
        Assert.True(vm.Relationship.TranslationEnabled);
        Assert.Equal("en", vm.Relationship.TargetCode);
    }

    [Fact]
    public async Task TranslationSwitch_RoutesThroughRelationship()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings);

        vm.TranslationSwitch = true;

        Assert.True(vm.Relationship.TranslationEnabled);
        Assert.Equal("en", vm.Relationship.TargetCode);
        Assert.Equal(true.ToString(),
            await settings.GetAsync<string>(SettingsKeys.TranslationEnabled, TestContext.Current.CancellationToken));

        vm.TranslationSwitch = false;
        Assert.False(vm.Relationship.TranslationEnabled);
    }

    // ----- Source picker: specials + sub-hints (FR-S1..S4) -----------------------

    [Fact]
    public async Task SourcePicker_LeadsWithKeyboardAndAutoSpecials()
    {
        var vm = await CreateAsync(
            new MockSettingsService(),
            layout: new KeyboardLayoutInfo("en", "English (United States)"));

        vm.OpenSourcePickerCommand.Execute(null);

        var keyboard = vm.SourcePicker.Items[0];
        Assert.Equal(LanguageCatalog.KeyboardLayoutCode, keyboard.Code);
        Assert.True(keyboard.IsSpecial);
        Assert.Equal("System keyboard layout", keyboard.DisplayName);
        Assert.Equal("Detected: English (United States)", keyboard.SecondaryText);

        var auto = vm.SourcePicker.Items[1];
        Assert.Equal(LanguageCatalog.AutoDetectCode, auto.Code);
        Assert.True(auto.IsSpecial);
        Assert.Equal("Let the model identify the language", auto.SecondaryText);
    }

    [Fact]
    public async Task SourcePicker_KeyboardSubHint_DegradesWhenDetectionUnavailable()
    {
        var vm = await CreateAsync(new MockSettingsService(), layout: null);

        vm.OpenSourcePickerCommand.Execute(null);

        Assert.Equal("Layout detection unavailable", vm.SourcePicker.Items[0].SecondaryText);
    }

    [Fact]
    public async Task SourcePicker_LongList_ShowsSearchAndGroupHeaders()
    {
        var vm = await CreateAsync(new MockSettingsService());

        vm.OpenSourcePickerCommand.Execute(null);

        Assert.True(vm.SourcePicker.ShowSearch);
        Assert.Contains(vm.SourcePicker.Items, i => i.IsHeader && i.DisplayName == "All languages");
        // No MRU yet → no Recent header.
        Assert.DoesNotContain(vm.SourcePicker.Items, i => i.IsHeader && i.DisplayName == "Recent");
    }

    [Fact]
    public async Task SourcePicker_RecentCluster_AppearsAfterSelection()
    {
        var vm = await CreateAsync(new MockSettingsService());

        vm.OpenSourcePickerCommand.Execute(null);
        vm.SourcePicker.SelectCommand.Execute("ru");
        vm.OpenSourcePickerCommand.Execute(null);

        var items = vm.SourcePicker.Items;
        var recentHeader = items.Select((item, idx) => (item, idx))
            .First(x => x.item.IsHeader && x.item.DisplayName == "Recent");
        Assert.Equal("ru", items[recentHeader.idx + 1].Code);
        Assert.True(items[recentHeader.idx + 1].IsRecent);
    }

    [Fact]
    public async Task SourcePicker_Filtering_HidesSpecialsAndHeaders()
    {
        var vm = await CreateAsync(new MockSettingsService());
        vm.OpenSourcePickerCommand.Execute(null);

        vm.SourcePicker.Filter = "russ";

        Assert.DoesNotContain(vm.SourcePicker.Items, i => i.IsSpecial);
        Assert.DoesNotContain(vm.SourcePicker.Items, i => i.IsHeader);
        Assert.Contains(vm.SourcePicker.Items, i => i.Code == "ru");
        Assert.False(vm.SourcePicker.HasNoResults);
    }

    [Fact]
    public async Task SourcePicker_EmptySearch_NamesTheQuery()
    {
        var vm = await CreateAsync(new MockSettingsService());
        vm.OpenSourcePickerCommand.Execute(null);

        vm.SourcePicker.Filter = "zzzz";

        Assert.True(vm.SourcePicker.HasNoResults);
        Assert.Equal("No languages match \"zzzz\".", vm.SourcePicker.NoResultsText);
    }

    [Fact]
    public async Task SelectSource_Keyboard_PersistsSentinel_ClosesPicker()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings, layout: new KeyboardLayoutInfo("de", "German (Germany)"));

        vm.OpenSourcePickerCommand.Execute(null);
        vm.SourcePicker.SelectCommand.Execute(LanguageCatalog.KeyboardLayoutCode);

        Assert.False(vm.SourcePicker.IsOpen);
        Assert.Equal(LanguageCatalog.KeyboardLayoutCode, vm.Relationship.SourceCode);
        Assert.Equal("⌨", vm.SourceTileText);
        Assert.Equal(LanguageCatalog.KeyboardLayoutCode,
            await settings.GetAsync<string>(SettingsKeys.SelectedSourceLanguage, TestContext.Current.CancellationToken));
    }

    // ----- Target picker: full form (FR-T3) ---------------------------------------

    [Fact]
    public async Task TargetPicker_FullForm_LeadsWithOffRow()
    {
        var vm = await CreateAsync(new MockSettingsService(), SpeechEngine.Gemma4);

        vm.OpenTargetPickerCommand.Execute(null);

        var off = vm.TargetPicker.Items[0];
        Assert.Equal(LanguageCatalog.NoTranslationCode, off.Code);
        Assert.True(off.IsSpecial);
        Assert.Equal("Off — no translation", off.DisplayName);
        // Translation is off → the Off row is the current selection.
        Assert.True(off.IsSelected);
    }

    [Fact]
    public async Task TargetPicker_SelectLanguage_EnablesTranslation_AndCloses()
    {
        var settings = new MockSettingsService();
        var vm = await CreateAsync(settings, SpeechEngine.Gemma4);

        vm.OpenTargetPickerCommand.Execute(null);
        vm.TargetPicker.SelectCommand.Execute("fr");

        Assert.False(vm.TargetPicker.IsOpen);
        Assert.True(vm.Relationship.TranslationEnabled);
        Assert.Equal("fr", vm.Relationship.TargetCode);
        Assert.Equal(["fr"],
            await settings.GetAsync<List<string>>(SettingsKeys.RecentTargetLanguages, TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task TargetPicker_SelectOff_DisablesTranslation()
    {
        var vm = await CreateAsync(new MockSettingsService(), SpeechEngine.Gemma4);
        vm.OpenTargetPickerCommand.Execute(null);
        vm.TargetPicker.SelectCommand.Execute("fr");

        vm.OpenTargetPickerCommand.Execute(null);
        vm.TargetPicker.SelectCommand.Execute(LanguageCatalog.NoTranslationCode);

        Assert.False(vm.Relationship.TranslationEnabled);
        Assert.Equal("fr", vm.Relationship.TargetCode); // resting target survives
        Assert.Equal("⊘", vm.TargetTileText);
    }

    [Fact]
    public async Task OpenTargetPicker_IsNoOp_OnToggleForm()
    {
        var vm = await CreateAsync(new MockSettingsService()); // Whisper

        vm.OpenTargetPickerCommand.Execute(null);

        Assert.False(vm.TargetPicker.IsOpen);
    }

    [Fact]
    public async Task Pickers_AreMutuallyExclusive()
    {
        var vm = await CreateAsync(new MockSettingsService(), SpeechEngine.Gemma4);

        vm.OpenSourcePickerCommand.Execute(null);
        Assert.True(vm.SourcePicker.IsOpen);

        vm.OpenTargetPickerCommand.Execute(null);
        Assert.True(vm.TargetPicker.IsOpen);
        Assert.False(vm.SourcePicker.IsOpen);
    }

    // ----- Engine switches (FR-M2) ------------------------------------------------

    [Fact]
    public async Task UpdateForEngine_FallbackSurfacesToast()
    {
        var vm = await CreateAsync(new MockSettingsService(), SpeechEngine.Gemma4);
        vm.OpenSourcePickerCommand.Execute(null);
        vm.SourcePicker.SelectCommand.Execute("ga"); // Irish: not in Whisper's set

        vm.UpdateForEngine(SpeechEngine.Whisper);

        Assert.Equal("Irish isn't a source in Whisper. Using your keyboard layout.",
            vm.Relationship.ToastMessage);
        Assert.Equal(LanguageCatalog.KeyboardLayoutCode, vm.Relationship.SourceCode);
    }

    [Fact]
    public async Task UpdateForEngine_ClosesTargetPopover_WhenFormStopsBeingFull()
    {
        var vm = await CreateAsync(new MockSettingsService(), SpeechEngine.Gemma4);
        vm.OpenTargetPickerCommand.Execute(null);
        Assert.True(vm.TargetPicker.IsOpen);

        vm.UpdateForEngine(SpeechEngine.Whisper);

        Assert.False(vm.TargetPicker.IsOpen);
    }

    [Fact]
    public async Task UpdateForEngine_RefreshesTargetList()
    {
        var vm = await CreateAsync(new MockSettingsService(), SpeechEngine.Gemma4);
        vm.OpenTargetPickerCommand.Execute(null);
        // Full catalog + Off special + "All languages" header.
        Assert.True(vm.TargetPicker.Items.Count > LanguageCatalog.AllLanguages.Count);

        vm.UpdateForEngine(SpeechEngine.Whisper);
        vm.UpdateForEngine(SpeechEngine.Gemma4);

        Assert.Contains(vm.TargetPicker.Items, i => i.Code == "fr");
    }

    // ----- ADR-033 paused note ------------------------------------------------------

    [Fact]
    public async Task UpdateTranslationAvailability_FlipsPausedNote()
    {
        var vm = await CreateAsync(new MockSettingsService());
        vm.ToggleTranslationCommand.Execute(null);

        vm.UpdateTranslationAvailability(WhisperModelType.LargeV3Turbo);
        Assert.True(vm.Relationship.ShowTranslationPausedNote);

        vm.UpdateTranslationAvailability(WhisperModelType.Medium);
        Assert.False(vm.Relationship.ShowTranslationPausedNote);
    }

    // ----- Legacy migration (ADR-034) ------------------------------------------------

    [Fact]
    public async Task Init_MigratesLegacyTranslateToEnglish_TurnsTranslationOn()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.TranslateToEnglish, true.ToString(), TestContext.Current.CancellationToken);

        var vm = await CreateAsync(settings);

        Assert.True(vm.Relationship.TranslationEnabled);
        Assert.Equal("en", vm.Relationship.TargetCode);
    }

    [Fact]
    public async Task Init_MigratesLegacyRecentLanguages_IntoSourceMru()
    {
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.RecentLanguages,
            new List<string> { "fr", "ru" }, TestContext.Current.CancellationToken);

        var vm = await CreateAsync(settings);
        vm.OpenSourcePickerCommand.Execute(null);

        var codes = vm.SourcePicker.Items.Where(i => i.IsRecent).Select(i => i.Code).ToList();
        Assert.Equal(["fr", "ru"], codes);
    }
}

/// <summary>
/// Picker building-block tests on a small synthetic catalog, where the real
/// engine catalogs are too large to exercise the short-list rules.
/// </summary>
public class LanguagePickerViewModelShortListTests
{
    private static readonly IReadOnlyList<LanguageInfo> ThreeLanguages =
    [
        new("en", "English", "English"),
        new("fr", "French", "Français"),
        new("de", "German", "Deutsch"),
    ];

    private static LanguagePickerViewModel CreatePicker(IReadOnlyList<LanguageInfo> supported) =>
        new(
            header: "Test",
            getSupported: () => supported,
            getRecents: () => ["fr"],
            getSelectedCode: () => "en",
            onSelect: _ => { });

    [Fact]
    public void ShortList_HidesSearch_AndGroupHeaders()
    {
        var picker = CreatePicker(ThreeLanguages);

        picker.Refresh();

        Assert.False(picker.ShowSearch);
        Assert.DoesNotContain(picker.Items, i => i.IsHeader);
        // Recent rows still float to the top, just without the header.
        Assert.Equal("fr", picker.Items[0].Code);
        Assert.True(picker.Items[0].IsRecent);
    }

    [Fact]
    public void LongList_ShowsSearch_AndGroupHeaders()
    {
        var picker = CreatePicker(LanguageCatalog.WhisperLanguages);

        picker.Refresh();

        Assert.True(picker.ShowSearch);
        Assert.Contains(picker.Items, i => i.IsHeader && i.DisplayName == "Recent");
        Assert.Contains(picker.Items, i => i.IsHeader && i.DisplayName == "All languages");
    }

    [Fact]
    public void SelectedCode_MarksRow_NotHeaders()
    {
        var picker = CreatePicker(ThreeLanguages);

        picker.Refresh();

        Assert.True(picker.Items.Single(i => i.Code == "en").IsSelected);
        Assert.Single(picker.Items, i => i.IsSelected);
    }

    [Fact]
    public void LanguageRows_CarryNativeSubname_OnlyWhenDiffering()
    {
        var picker = CreatePicker(ThreeLanguages);

        picker.Refresh();

        var english = picker.Items.Single(i => i.Code == "en");
        Assert.False(english.HasSecondaryText);

        var french = picker.Items.Single(i => i.Code == "fr");
        Assert.Equal("Français", french.SecondaryText);

        // Tile shows the upper-cased code for plain language rows.
        Assert.Equal("FR", french.TileText);
    }
}
