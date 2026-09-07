using System.Globalization;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Parlotype.Core.Hotkeys;
using Parlotype.Core.Localization;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Parlotype.Desktop.Views;
using Parlotype.Desktop.Views.Settings;
using Parlotype.Platform.Settings;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The localization mechanism itself (ADR-064): resource lookup, the live switch,
/// and the culture-resolution rules.
/// </summary>
/// <remarks>
/// Every test here is an <c>[AvaloniaFact]</c>, so it runs on the Avalonia UI
/// thread — the thread the app itself always calls <c>SetCulture</c> from.
/// Running them as plain <c>[Fact]</c>s put the culture change on an xUnit worker
/// thread, where notifications reached bindings left behind by earlier UI tests
/// and failed with a thread-affinity error under load: the suite passed alone and
/// failed intermittently in a full run.
///
/// These tests mutate <see cref="Localizer"/>, which is process-wide static state,
/// so they run in one collection and restore English on the way out. Everything
/// else in the suite renders under the neutral culture and would see a stale
/// language otherwise.
/// </remarks>
[Collection(CultureBoundTests.Name)]
public class LocalizationTests : IDisposable
{
    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");
    private static readonly CultureInfo Russian = CultureInfo.GetCultureInfo("ru");

    public void Dispose() => Localizer.Instance.SetCulture(English);

    [AvaloniaFact]
    public void Lookup_UsesTheSatelliteForTheCurrentCulture()
    {
        Localizer.Instance.SetCulture(English);
        Assert.Equal("Theme", Strings.Settings_Theme_Title);

        Localizer.Instance.SetCulture(Russian);
        Assert.Equal("Тема", Strings.Settings_Theme_Title);
    }

    [AvaloniaFact]
    public void Lookup_FallsBackToTheKeyName_WhenTheKeyIsMissing()
    {
        // ADR-056's rule: a stale resx shows an obviously wrong label rather than
        // taking the window down.
        Assert.Equal("No_Such_Key_Exists", Localizer.Lookup("No_Such_Key_Exists"));
    }

    [AvaloniaFact]
    public void SetCulture_LeavesNumberAndDateFormattingAlone()
    {
        var before = CultureInfo.CurrentCulture;

        Localizer.Instance.SetCulture(Russian);

        // The interface language is not a claim about where the user is: dates and
        // numbers keep following their Windows regional settings.
        Assert.Equal(before, CultureInfo.CurrentCulture);
        Assert.Equal(Russian, CultureInfo.CurrentUICulture);
    }

    [AvaloniaFact]
    public void Entry_ReturnsTheSameInstancePerKey()
    {
        // One object and one notification per key, however many controls bind it.
        Assert.Same(
            Localizer.Instance.Entry(nameof(Strings.Settings_Theme_Title)),
            Localizer.Instance.Entry(nameof(Strings.Settings_Theme_Title)));
    }

    [AvaloniaFact]
    public void Entry_RaisesPropertyChanged_WhenTheCultureChanges()
    {
        Localizer.Instance.SetCulture(English);
        var entry = Localizer.Instance.Entry(nameof(Strings.Settings_Theme_Title));

        var raised = 0;
        entry.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(LocalizedString.Value))
                raised++;
        };

        Localizer.Instance.SetCulture(Russian);

        Assert.Equal(1, raised);
        Assert.Equal("Тема", entry.Value);
    }

    [AvaloniaFact]
    public void TrMarkupExtension_UpdatesBoundTextInPlace_WithoutRestart()
    {
        // The whole point of the live-switch decision: text already on screen has
        // to change, not just text built after the switch.
        Localizer.Instance.SetCulture(English);

        var view = new InterfaceLanguageSettingsView
        {
            DataContext = new InterfaceLanguageSettingsViewModel(
                new UiLanguageService(new MockSettingsService())),
        };
        var window = new Window { Content = view };
        window.Show();

        var title = view.GetVisualDescendants().OfType<TextBlock>().First();
        Assert.Equal("Interface language", title.Text);

        Localizer.Instance.SetCulture(Russian);

        Assert.Equal("Язык интерфейса", title.Text);

        window.Close();
    }

    [AvaloniaFact]
    public void SettingsSection_RaisesItsTitle_WhenTheCultureChanges()
    {
        Localizer.Instance.SetCulture(English);
        var section = new ThemeSettingsViewModel(new MockSettingsService());

        var titleRaised = false;
        section.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeSettingsViewModel.Title))
                titleRaised = true;
        };

        Localizer.Instance.SetCulture(Russian);

        Assert.True(titleRaised, "sections must re-raise Title so the nav list can rebuild");
        Assert.Equal("Тема", section.Title);
        Assert.Equal("Тёмная", section.ThemeOptions.Single(o => o.Theme == AppTheme.Dark).DisplayName);
    }

    [AvaloniaFact]
    public void TrayMenuLabels_FollowTheInterfaceLanguage()
    {
        // The tray is the one surface a live switch cannot reach on its own:
        // NativeMenu is built once when App.axaml loads and never rebuilt, so its
        // headers bind to AppViewModel properties that have to be re-raised.
        Localizer.Instance.SetCulture(English);
        var app = new AppViewModel(new MockWindowManager());

        Assert.Equal("Exit", app.TrayExitLabel);

        var raised = new List<string?>();
        app.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Localizer.Instance.SetCulture(Russian);

        Assert.Equal("Выход", app.TrayExitLabel);
        Assert.Equal("Открыть", app.TrayOpenLabel);
        Assert.Contains(nameof(AppViewModel.TrayExitLabel), raised);
        Assert.Contains(nameof(AppViewModel.TrayOpenLabel), raised);
        Assert.Contains(nameof(AppViewModel.TraySettingsLabel), raised);
    }

    [AvaloniaFact]
    public void ModelDownloadPrompt_UsesTheTranslatedCompositeFormat()
    {
        Localizer.Instance.SetCulture(Russian);

        var vm = ModelDownloadViewModel.ForWhisperModel("Large v3", "1.5 GB");

        // The model name and size are substituted, not translated.
        Assert.Equal("Скачать «Large v3» (1.5 GB) из интернета?", vm.StatusText);
        Assert.Equal("Загрузка модели", vm.Title);
    }

    [AvaloniaFact]
    public void SettingsNavigation_RebuildsInTheNewLanguage()
    {
        // Nav rows are snapshots of Section.Title and the category display names,
        // so nothing about them is bound — SettingsWindowViewModel has to rebuild
        // the list on CultureChanged or the whole left pane stays English.
        Localizer.Instance.SetCulture(English);
        var vm = SettingsWindowViewModelFactory.Build();

        Assert.Contains(vm.NavItems, n => n.Label == "Audio" && n.IsHeader);
        Assert.Contains(vm.NavItems, n => n.Label == "Microphone" && !n.IsHeader);

        Localizer.Instance.SetCulture(Russian);

        Assert.Contains(vm.NavItems, n => n.Label == "Звук" && n.IsHeader);
        Assert.Contains(vm.NavItems, n => n.Label == "Микрофон" && !n.IsHeader);
        Assert.DoesNotContain(vm.NavItems, n => n.Label == "Microphone");
    }

    [AvaloniaFact]
    public void SettingsNavigation_KeepsTheSelectedSection_AcrossALanguageChange()
    {
        // The rebuild throws every row away, so selection has to survive by
        // section identity rather than by label — otherwise switching language
        // silently navigates the user back to the first page.
        Localizer.Instance.SetCulture(English);
        var vm = SettingsWindowViewModelFactory.Build();

        var target = vm.NavItems.First(n => !n.IsHeader && n.Label == "Data");
        vm.SelectedNavItem = target;
        var section = vm.SelectedSection;

        Localizer.Instance.SetCulture(Russian);

        Assert.Same(section, vm.SelectedSection);
        Assert.Equal("Данные", vm.SelectedNavItem!.Label);
    }

    [AvaloniaFact]
    public void SettingsWindow_DoesNotRecreateTheContentView_WhenInterfaceLanguageChanges()
    {
        // Reproduces the reported flicker with the real SettingsWindow markup,
        // not just the view model: its ListBox binds SelectedItem two-way to
        // SelectedNavItem, and its ContentControl binds Content to
        // SelectedSection. If RebuildNavItems ever calls NavItems.Clear()
        // before re-adding rows, the ListBox reacts to the ItemsSource reset by
        // deselecting — SelectedNavItem goes to null, SelectedSection follows,
        // and the ContentControl tears down and reconstructs its templated
        // child from scratch. That freshly-built InterfaceLanguageSettingsView
        // — new Buttons with no applied visual state yet — is what reads as
        // the picker's rows flashing every time the culture changes
        // (ADR-064 amendment).
        Localizer.Instance.SetCulture(English);
        var vm = SettingsWindowViewModelFactory.Build();
        var window = new SettingsWindow { DataContext = vm };
        window.Show();

        var target = vm.NavItems.First(n => !n.IsHeader && n.Label == "Interface language");
        vm.SelectedNavItem = target;

        var contentControl = window.GetVisualDescendants().OfType<ContentControl>()
            .First(c => c.Content is InterfaceLanguageSettingsViewModel);
        var viewBefore = contentControl.GetVisualDescendants()
            .OfType<InterfaceLanguageSettingsView>().SingleOrDefault();
        Assert.NotNull(viewBefore);

        Localizer.Instance.SetCulture(Russian);

        var viewAfter = contentControl.GetVisualDescendants()
            .OfType<InterfaceLanguageSettingsView>().SingleOrDefault();
        Assert.Same(viewBefore, viewAfter);

        window.Close();
    }

    [AvaloniaFact]
    public void SelectLanguageCommand_IsSynchronous_SoItDoesNotDisableEveryRowWhileApplying()
    {
        // The real cause of the reported flicker: every row's Button shares the
        // SAME SelectLanguageCommand instance (built once in the constructor and
        // handed to every UiLanguageDisplayItem). Had that command been declared
        // `async Task`, CommunityToolkit.Mvvm's source generator would produce
        // an AsyncRelayCommand whose CanExecute returns false for as long as it
        // is running (IsRunning) unless AllowConcurrentExecutions is set — and
        // since Avalonia's Button re-evaluates CanExecute on CanExecuteChanged,
        // every row bound to the shared command disables and re-enables
        // together for the length of the settings write. That is a uniform
        // flash across the whole list, not a rebuild of any view — which is why
        // fixing the ContentControl-recreation bug above did not make it go
        // away. A plain synchronous RelayCommand never touches IsRunning, so
        // CanExecute — and every bound row's IsEnabled — never changes. See
        // memory/knowledge/asyncrelaycommand-flicker.md.
        var vm = new InterfaceLanguageSettingsViewModel(new UiLanguageService(new MockSettingsService()));

        Assert.IsNotAssignableFrom<IAsyncRelayCommand>(vm.LanguageOptions[0].SelectCommand);
    }

    [AvaloniaFact]
    public void RuntimeRestartNote_IsOneTranslatableSentence()
    {
        // It used to be five <Run>s with the runtime names interleaved — a
        // sentence no translator can reorder (ADR-064).
        Localizer.Instance.SetCulture(Russian);
        var vm = new RuntimeSettingsViewModel(new MockSettingsService(), new MockVulkanEnvironmentProvider())
        {
            LoadedRuntimeName = "Vulkan",
            SelectedRuntime = RuntimePreference.Cpu,
        };

        var note = vm.RestartRequiredNote;

        Assert.StartsWith("В этом сеансе уже работает Vulkan.", note);
        Assert.Contains("Cpu", note);        // runtime names are never translated
        Assert.DoesNotContain("{0}", note);
    }

    // ---- code review: several C#-composed surfaces did not update on a live
    // language switch, and one page's picker copy was never localized at all
    // (ADR-064 amendment) --------------------------------------------------

    [AvaloniaFact]
    public async Task LanguagePicker_HeadersAndSpecials_RefreshOnCultureChange()
    {
        // These were hardcoded English C# strings in
        // LanguageSelectionSettingsViewModel/LanguageRowFactory, never routed
        // through Strings at all — so simply switching culture proves both
        // that they're localized now and that the picker's Items/Header
        // rebuild rather than staying snapshotted from construction.
        Localizer.Instance.SetCulture(English);

        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(), TestContext.Current.CancellationToken);
        var relationship = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await relationship.InitializeAsync(TestContext.Current.CancellationToken);
        var vm = new LanguageSelectionSettingsViewModel(relationship);

        relationship.SelectSource("fr"); // seeds the Recent cluster
        relationship.ToggleTranslation();
        vm.OpenSourcePickerCommand.Execute(null);

        Assert.Equal("You speak", vm.SourcePicker.Header);
        Assert.Contains(vm.SourcePicker.Items, i => i.IsHeader && i.DisplayName == "Recent");
        Assert.Contains(vm.SourcePicker.Items, i => i.IsHeader && i.DisplayName == "All languages");
        Assert.Contains(vm.SourcePicker.Items,
            i => i.Code == LanguageCatalog.KeyboardLayoutCode && i.DisplayName == "System keyboard layout");
        Assert.Equal("Translation target", vm.TargetSubHint);

        Localizer.Instance.SetCulture(Russian);

        Assert.Equal("Вы говорите", vm.SourcePicker.Header);
        Assert.Contains(vm.SourcePicker.Items, i => i.IsHeader && i.DisplayName == "Недавние");
        Assert.Contains(vm.SourcePicker.Items, i => i.IsHeader && i.DisplayName == "Все языки");
        Assert.Contains(vm.SourcePicker.Items,
            i => i.Code == LanguageCatalog.KeyboardLayoutCode && i.DisplayName == "Раскладка клавиатуры");
        Assert.Equal("Язык перевода", vm.TargetSubHint);
    }

    [AvaloniaFact]
    public async Task LanguageRelationship_LocalizedDerivedProperties_RefreshOnCultureChange()
    {
        // LanguageRelationshipViewModel is shared by two surfaces but is not a
        // settings section, so it has no OnCultureChanged hook — it needed its
        // own subscription. Checking that PropertyChanged actually fires (not
        // just that a fresh read returns the right text) is what distinguishes
        // this from a no-op: every property here recomputes correctly on its
        // own, the bug was that nothing told a bound view to re-read it.
        Localizer.Instance.SetCulture(English);

        var settings = new MockSettingsService();
        var vm = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await vm.InitializeAsync(TestContext.Current.CancellationToken);

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Localizer.Instance.SetCulture(Russian);

        Assert.Contains(nameof(LanguageRelationshipViewModel.SourceDisplayLabel), raised);
        Assert.Contains(nameof(LanguageRelationshipViewModel.SourceSubHint), raised);
        Assert.Contains(nameof(LanguageRelationshipViewModel.SummaryText), raised);
        Assert.Contains(nameof(LanguageRelationshipViewModel.ConnectorTooltip), raised);
    }

    [AvaloniaFact]
    public void SpeechEngineCards_RefreshDisplayText_OnCultureChange()
    {
        // Every card's DisplayName/Description was captured once, into an
        // immutable property, at construction.
        Localizer.Instance.SetCulture(English);
        var vm = new SpeechEngineSettingsViewModel(new MockSettingsService());
        var whisperCard = vm.EngineOptions.Single(o => o.Type == SpeechEngine.Whisper);
        var englishDescription = whisperCard.Description;

        Localizer.Instance.SetCulture(Russian);

        // "Whisper" itself is an engine identifier and stays untranslated in
        // every language (CLAUDE.md) — the description is ordinary prose.
        Assert.NotEqual(englishDescription, whisperCard.Description);
        Assert.Equal(Strings.Settings_Engine_Whisper_Name, whisperCard.DisplayName);
        Assert.Equal(Strings.Settings_Engine_Whisper_Description, whisperCard.Description);
    }

    [AvaloniaFact]
    public void RuntimeCards_AndRestartNote_RefreshOnCultureChange()
    {
        Localizer.Instance.SetCulture(English);
        var vm = new RuntimeSettingsViewModel(new MockSettingsService(), new MockVulkanEnvironmentProvider())
        {
            LoadedRuntimeName = "Vulkan",
            SelectedRuntime = RuntimePreference.Cpu,
        };
        var autoCard = vm.RuntimeOptions.Single(o => o.Type == RuntimePreference.Auto);

        var raised = new List<string?>();
        vm.PropertyChanged += (_, e) => raised.Add(e.PropertyName);

        Localizer.Instance.SetCulture(Russian);

        Assert.Equal(Strings.Settings_Runtime_Auto_Name, autoCard.DisplayName);
        Assert.Equal(Strings.Settings_Runtime_Auto_Description, autoCard.Description);
        Assert.Contains(nameof(RuntimeSettingsViewModel.RestartRequiredNote), raised);
    }

    [AvaloniaFact]
    public async Task TranscribeWidget_StatusAndCloudBadge_RefreshOnCultureChange()
    {
        // StatusText is a plain stored string, not a computed property — it
        // has to remember *what it's currently saying* (StatusKind) to
        // recompute correctly rather than defaulting back to "Ready" and
        // silently discarding whatever state was actually being shown.
        Localizer.Instance.SetCulture(English);
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(new MockWindowManager(), pipeline);
        await vm.StartRecordingAsync();

        Assert.Equal("Recording...", vm.StatusText);

        Localizer.Instance.SetCulture(Russian);

        Assert.Equal("Запись...", vm.StatusText);
    }

    [AvaloniaFact]
    public void HotkeySection_RefreshesRowsAndWarnings_OnCultureChange()
    {
        // Synchronous on purpose, unlike HotkeySettingsViewModelTests' usual
        // await Task.Delay(50) + RunJobs() dance: that real-time delay lets the
        // continuation resume off the Avalonia dispatcher thread, and this test
        // calls Localizer.Instance.SetCulture from there — racing whatever
        // other [AvaloniaFact] test happens to be mid-flight on the shared
        // headless dispatcher at that moment (culture-changing-tests-need-
        // avaloniafact.md). MockSettingsService completes every Task
        // synchronously, so the constructor's fire-and-forget InitializeAsync
        // has already populated Bindings by the time the constructor returns —
        // no delay needed.
        Localizer.Instance.SetCulture(English);
        var vm = new HotkeySettingsViewModel(hotkeyService: null, new MockSettingsService());

        vm.ApplyRecordedChord(new HotkeyBinding(HotkeyModifiers.Meta, "L")); // reserved chord
        Assert.NotNull(vm.BlockingWarning);
        var englishWarning = vm.BlockingWarning;

        var preset = vm.Presets[0];
        var presetRaised = new List<string?>();
        preset.PropertyChanged += (_, e) => presetRaised.Add(e.PropertyName);

        Assert.NotEmpty(vm.Bindings);
        var binding = vm.Bindings[0];
        var bindingRaised = new List<string?>();
        binding.PropertyChanged += (_, e) => bindingRaised.Add(e.PropertyName);

        Localizer.Instance.SetCulture(Russian);

        // Rows compute their display strings fresh on every read regardless of
        // this fix, so the read alone would pass either way — the actual bug
        // was that nothing told a bound view to re-read them.
        Assert.Contains(nameof(HotkeyPresetViewModel.DisplayString), presetRaised);
        Assert.Contains(nameof(HotkeyBindingItemViewModel.DisplayString), bindingRaised);
        Assert.NotEqual(englishWarning, vm.BlockingWarning);
        Assert.Equal(Strings.Settings_Hotkeys_Recorder_Idle, vm.RecorderText);
    }

    [AvaloniaFact]
    public void HotkeyHint_IsLocalized_ForEveryGestureKindAndMode()
    {
        // The record-button tooltip / onboarding recap line was built entirely
        // in Core (HotkeyHint.Describe), which has no resources — hardcoded
        // English regardless of the interface language (reported by the user
        // against a screenshot of the widget). Core now only picks *which*
        // binding to describe (HotkeyHint.SelectPrimary); the words are here.
        Localizer.Instance.SetCulture(English);
        Assert.Equal(
            "Hold Right Ctrl to talk · Esc to cancel",
            HotkeyText.Hint(DictationHotkeyDefaults.All));
        Assert.Equal(
            "Double-tap Ctrl to dictate · Esc to cancel",
            HotkeyText.Hint([DictationHotkeyDefaults.Toggle]));
        Assert.Equal("No dictation hotkey set", HotkeyText.Hint([]));

        Localizer.Instance.SetCulture(Russian);
        Assert.Equal(
            "Удержание правого Ctrl — чтобы говорить · Esc — отменить",
            HotkeyText.Hint(DictationHotkeyDefaults.All));
        Assert.Equal(
            "Двойное нажатие Ctrl — чтобы диктовать · Esc — отменить",
            HotkeyText.Hint([DictationHotkeyDefaults.Toggle]));
        Assert.Equal("Горячая клавиша диктовки не задана", HotkeyText.Hint([]));

        Localizer.Instance.SetCulture(English);
        var chord = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Ctrl | HotkeyModifiers.Alt, "Space"),
            ActivationMode.PushToTalk);
        Assert.Equal(
            "Ctrl+Alt+Space to talk · Esc to cancel",
            HotkeyText.Hint([chord]));
    }

    [AvaloniaFact]
    public async Task HotkeyCoordinator_RefreshesTheWidgetTooltip_OnCultureChange()
    {
        // The tooltip is pushed into TranscribeViewModel.HotkeyHintText by
        // HotkeyCoordinator, not computed by the view model itself, so it
        // needed its own Localizer.CultureChanged subscription.
        Localizer.Instance.SetCulture(English);
        var hotkey = new MockGlobalHotkeyService();
        var wm = new MockWindowManager();
        var vm = new TranscribeViewModel(wm);
        using var coordinator = new HotkeyCoordinator(
            wm, vm, NullLogger<HotkeyCoordinator>.Instance, hotkey);
        await coordinator.StartAsync(TestContext.Current.CancellationToken);

        Assert.Equal("Hold Right Ctrl to talk · Esc to cancel", vm.HotkeyHintText);

        Localizer.Instance.SetCulture(Russian);
        Avalonia.Threading.Dispatcher.UIThread.RunJobs(); // drains the Post()ed refresh

        Assert.Equal("Удержание правого Ctrl — чтобы говорить · Esc — отменить", vm.HotkeyHintText);
    }

    [AvaloniaFact]
    public async Task LanguageSummary_ComposesNestedFormats_InTheChosenLanguage()
    {
        // The summary nests one composite format inside another: the paused
        // suffix wraps the spoken language, and the result is substituted into
        // the "you speak X, Parlotype types Y" sentence. If either level is
        // rebuilt with interpolation instead of a format, this breaks.
        Localizer.Instance.SetCulture(Russian);

        // Whisper, because the paused state only exists on the toggle form — the
        // default engine (Parakeet) cannot translate at all.
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Whisper.ToString(), TestContext.Current.CancellationToken);
        var vm = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await vm.InitializeAsync(TestContext.Current.CancellationToken);
        vm.SelectSource("ru");
        vm.ToggleTranslation();
        vm.SetWhisperModel(WhisperModelType.LargeV3Turbo);

        Assert.True(vm.IsTranslationPaused);
        Assert.Equal(
            "Вы говорите: Russian → Parlotype печатает: Russian (перевод приостановлен)",
            vm.SummaryText);
    }

    [AvaloniaFact]
    public async Task LanguageToast_SubstitutesThreeValues_WithoutLeavingPlaceholders()
    {
        // The three-slot toast is the one most likely to be miscounted, and a
        // wrong arity would throw FormatException at the moment a user switches
        // engine rather than at build time.
        Localizer.Instance.SetCulture(Russian);

        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.SpeechEngine, SpeechEngine.Gemma4.ToString(), TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.SelectedTargetLanguage, "fr", TestContext.Current.CancellationToken);
        await settings.SetAsync(SettingsKeys.TranslationEnabled, true.ToString(), TestContext.Current.CancellationToken);

        var vm = new LanguageRelationshipViewModel(settings, new MockKeyboardLayoutService());
        await vm.InitializeAsync(TestContext.Current.CancellationToken);

        // Whisper only translates to English, so the French target cannot survive.
        vm.SetEngine(SpeechEngine.Whisper);

        Assert.NotNull(vm.ToastMessage);
        Assert.Equal("French недоступен в Whisper. Язык перевода — English.", vm.ToastMessage);
    }

    [AvaloniaFact]
    public void LanguagePicker_NoResults_QuotesTheSearchTermInTheLocalePunctuation()
    {
        Localizer.Instance.SetCulture(Russian);

        var picker = new LanguagePickerViewModel(
            getHeader: () => "x",
            getSupported: () => [],
            getRecents: () => [],
            getSelectedCode: () => null,
            onSelect: _ => { },
            getSpecials: () => [])
        {
            Filter = "zzz",
        };

        // Russian uses guillemets, not the straight quotes the English string has.
        Assert.Equal("По запросу «zzz» языков не найдено.", picker.NoResultsText);
    }

    [AvaloniaFact]
    public void HotkeyText_TranslatesTheVerbs_ButNeverTheKeyNames()
    {
        Localizer.Instance.SetCulture(Russian);

        var hold = DictationHotkey.Hold(ModifierKey.Ctrl, ModifierSide.Right);

        // The verb and the side are prose; "Ctrl" is what is printed on the key.
        Assert.Equal("Удержание правого Ctrl", HotkeyText.Gesture(hold.Gesture));
        Assert.Equal("Удержание", HotkeyText.Mode(hold.Mode));

        // Core keeps the invariant form, because the logs grep it.
        Assert.Equal("Hold Right Ctrl", hold.DisplayString);
    }

    [AvaloniaFact]
    public async Task ChosenLanguage_IsPersisted_AndAppliedOnTheNextStartup()
    {
        var settings = new MockSettingsService();
        var service = new UiLanguageService(settings);

        await service.SetLanguageAsync("ru", TestContext.Current.CancellationToken);
        Assert.Equal("ru", await settings.GetAsync<string>(SettingsKeys.UiLanguage, TestContext.Current.CancellationToken));

        Localizer.Instance.SetCulture(English);
        await service.ApplyStartupLanguageAsync(TestContext.Current.CancellationToken);

        Assert.Equal("ru", Localizer.Instance.CurrentCulture.Name);
    }

    [AvaloniaFact]
    public void StartupLanguage_LoadsOnAUiThread_WithoutDeadlocking()
    {
        // Guards the startup sequence in App.OnFrameworkInitializationCompleted.
        // JsonSettingsService awaits without ConfigureAwait(false), so awaiting it
        // and blocking on the result from a thread that carries a synchronisation
        // context — which an [AvaloniaFact] does, exactly like the real UI thread —
        // deadlocks. Reading through Task.Run and applying afterwards is what makes
        // it safe, and this test fails by hanging if that is ever undone.
        using var paths = new MockAppPaths();
        Directory.CreateDirectory(paths.SettingsDirectory);

        // The file has to exist, or the read completes synchronously, never
        // suspends, and the deadlock this test guards against cannot happen.
        File.WriteAllText(paths.SettingsFilePath, """{"UiLanguage":"ru"}""");

        var settings = new JsonSettingsService(paths, NullLogger<JsonSettingsService>.Instance);
        var service = new UiLanguageService(settings);

        var stored = Task.Run(() => service.ReadStoredLanguageAsync())
            .GetAwaiter()
            .GetResult();
        service.ApplyLanguage(stored);

        Assert.Equal("ru", stored);
        // Applied on this thread — the one that reads resources.
        Assert.Equal("ru", Localizer.Instance.CurrentCulture.Name);
        Assert.Equal("Тема", Strings.Settings_Theme_Title);
    }

    [AvaloniaFact]
    public async Task StoredLanguageWeNoLongerShip_FallsBackToTheSystem()
    {
        // A language dropped in a later release must not pin the user to a culture
        // with no resources behind it.
        var settings = new MockSettingsService();
        await settings.SetAsync(SettingsKeys.UiLanguage, "kl", TestContext.Current.CancellationToken);

        Assert.Equal(
            SupportedUiLanguages.SystemSettingValue,
            await new UiLanguageService(settings).GetStoredSettingValueAsync(TestContext.Current.CancellationToken));
    }

    // ---- messages Core classifies and Desktop words (ADR-064 amendment) ----
    //
    // These loops are exhaustive over the Core enums on purpose. A member added
    // without its resx key falls through to the invariant English wording, and
    // no parity check can catch that: the key is missing from every language at
    // once, so the languages still agree with each other. Asking for Cyrillic in
    // the Russian rendering is what makes the omission visible - it fails both
    // when the key is absent (Localizer returns the key name) and when the
    // mapper falls back to Core's English.

    private static void AssertReadsAsRussian(string? text, string what)
    {
        Assert.False(string.IsNullOrWhiteSpace(text), $"{what} produced no text at all.");
        Assert.True(
            text!.Any(c => c is >= '\u0400' and <= '\u04FF'),
            $"{what} is still English under ru: \"{text}\". Add its key to all three resx files.");
    }

    [AvaloniaFact]
    public void EveryReservedShortcut_IsTranslated()
    {
        Localizer.Instance.SetCulture(Russian);

        foreach (var shortcut in Enum.GetValues<ReservedShortcut>())
            AssertReadsAsRussian(HotkeyText.Reserved(shortcut), $"ReservedShortcut.{shortcut}");
    }

    [AvaloniaFact]
    public void EveryHotkeyConflictReason_IsTranslated()
    {
        Localizer.Instance.SetCulture(Russian);

        var candidate = DictationHotkey.Chord(
            new HotkeyBinding(HotkeyModifiers.Meta, "L"), ActivationMode.Toggle);

        foreach (var reason in Enum.GetValues<HotkeyConflictReason>())
        {
            if (reason == HotkeyConflictReason.None)
                continue;

            var conflict = reason switch
            {
                HotkeyConflictReason.InvalidCombination => HotkeyConflict.Invalid("invariant"),
                HotkeyConflictReason.AlreadyBound =>
                    HotkeyConflict.AlreadyBound("invariant", ActivationMode.PushToTalk),
                HotkeyConflictReason.Reserved =>
                    HotkeyConflict.ReservedBy("invariant", ReservedShortcut.LockWorkstation),
                _ => HotkeyConflict.Warning("invariant", reason),
            };

            AssertReadsAsRussian(
                HotkeyText.Conflict(candidate, conflict), $"HotkeyConflictReason.{reason}");
        }
    }

    [AvaloniaFact]
    public void EveryCloudFailure_IsTranslated()
    {
        Localizer.Instance.SetCulture(Russian);

        foreach (var kind in Enum.GetValues<CloudSpeechErrorKind>())
        {
            var ex = new CloudSpeechTranscriptionException(
                kind, SpeechEngine.OpenAiCompatible, "OpenAI-compatible provider",
                "invariant", 429, "provider said so");
            AssertReadsAsRussian(CloudErrorText.Transcription(ex), $"CloudSpeechErrorKind.{kind}");
        }

        foreach (var error in Enum.GetValues<CloudBaseUrlError>())
        {
            AssertReadsAsRussian(
                CloudErrorText.BaseUrl(new CloudBaseUrlFailure(error, "ftp")),
                $"CloudBaseUrlError.{error}");
        }

        AssertReadsAsRussian(
            CloudErrorText.NotConfigured(new CloudProviderNotConfiguredException(
                SpeechEngine.XaiGrok, CloudConfigurationError.MissingApiKey, "invariant")),
            "CloudConfigurationError.MissingApiKey");

        AssertReadsAsRussian(
            CloudErrorText.NotConfigured(new CloudProviderNotConfiguredException(
                SpeechEngine.OpenAiCompatible, CloudConfigurationError.InvalidBaseUrl, "invariant",
                new CloudBaseUrlFailure(CloudBaseUrlError.PlainHttpNotLoopback))),
            "CloudConfigurationError.InvalidBaseUrl");
    }

    [AvaloniaFact]
    public void TheProviderNameNeverNeedsAGrammaticalCase()
    {
        // Every Cloud_* format opens with the provider slot, so translations can
        // keep the name in the nominative. If a format ever moves the slot into
        // the middle of a sentence, Russian would need to inflect a name it is
        // handed verbatim - this pins the shape instead.
        Localizer.Instance.SetCulture(Russian);

        var provider = CloudErrorText.Provider(SpeechEngine.OpenAiCompatible);
        var message = CloudErrorText.NotConfigured(new CloudProviderNotConfiguredException(
            SpeechEngine.OpenAiCompatible, CloudConfigurationError.MissingApiKey, "invariant"));

        Assert.StartsWith(provider, message, StringComparison.Ordinal);
    }
}
