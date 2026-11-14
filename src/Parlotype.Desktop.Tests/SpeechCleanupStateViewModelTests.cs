using System.Globalization;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;
using Parlotype.Desktop.Tests.Mocks;
using Parlotype.Desktop.ViewModels;
using Parlotype.Desktop.ViewModels.Settings;
using Xunit;

namespace Parlotype.Desktop.Tests;

/// <summary>
/// The shared speech cleanup state (ADR-075): what the Transcribe widget's
/// indicator shows, when cleanup is paused for the dictation language, and that
/// the widget and the settings page follow each other.
/// </summary>
[Collection(CultureBoundTests.Name)]
public sealed class SpeechCleanupStateViewModelTests : IDisposable
{
    private const string InstallId = "b200-win-cpu-x64";

    private static readonly CultureInfo English = CultureInfo.GetCultureInfo("en");

    private readonly MockSettingsService _settings = new();
    private readonly MockSpeechCleanupModelInstaller _model = new();
    private readonly MockLlamaServerRegistry _registry = new();
    private readonly MockSpeechCleanupService _cleanup;

    public SpeechCleanupStateViewModelTests()
    {
        Localizer.Instance.SetCulture(English);
        _cleanup = new MockSpeechCleanupService(_model, _registry);
    }

    public void Dispose() => Localizer.Instance.SetCulture(English);

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    /// <summary>Puts the model and an active server on the fake disk.</summary>
    private async Task InstallAsync()
    {
        _model.Installed = true;
        _registry.AddManaged(InstallId);
        await _registry.SetActiveAsync(InstallId, LlamaServerSource.Managed, Ct);
    }

    private Task SwitchOnAsync() =>
        _settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, bool.TrueString, Ct);

    private async Task<LanguageRelationshipViewModel> RelationshipAsync(SpeechEngine engine)
    {
        await _settings.SetAsync(SettingsKeys.SpeechEngine, engine.ToString(), Ct);
        var relationship = new LanguageRelationshipViewModel(_settings, new MockKeyboardLayoutService());
        await relationship.InitializeAsync(Ct);
        return relationship;
    }

    private async Task<SpeechCleanupStateViewModel> NewStateAsync(LanguageRelationshipViewModel? relationship = null)
    {
        var state = new SpeechCleanupStateViewModel(_settings, _cleanup, relationship);
        await state.Initialization;
        return state;
    }

    // --- The indicator ---

    [AvaloniaFact]
    public async Task NeverSetUp_ShowsNothing()
    {
        var state = await NewStateAsync();

        Assert.Equal(CleanupIndicatorState.Hidden, state.State);
        Assert.False(state.IsIndicatorVisible);
    }

    [AvaloniaFact]
    public async Task InstalledButSwitchedOff_IsOff_AndOneClickTurnsItOn()
    {
        await InstallAsync();
        var state = await NewStateAsync();

        Assert.Equal(CleanupIndicatorState.Off, state.State);
        Assert.Equal(Strings.Transcribe_Cleanup_Tooltip_Off, state.Tooltip);

        Assert.True(await state.SetEnabledAsync(true));

        Assert.Equal(CleanupIndicatorState.On, state.State);
        Assert.Equal(Strings.Transcribe_Cleanup_Tooltip_On, state.Tooltip);
        Assert.Equal(bool.TrueString, await _settings.GetAsync<string>(SettingsKeys.SpeechCleanupEnabled, Ct));
    }

    [AvaloniaFact]
    public async Task TurningOn_IsRefused_WhileSomethingIsMissing()
    {
        // The downloads belong to the settings page, which asks first.
        var state = await NewStateAsync();

        Assert.False(await state.SetEnabledAsync(true));

        Assert.False(state.IsEnabled);
        Assert.Null(await _settings.GetAsync<string>(SettingsKeys.SpeechCleanupEnabled, Ct));
    }

    [AvaloniaFact]
    public async Task TurningOff_FreesTheModel()
    {
        await InstallAsync();
        await SwitchOnAsync();
        var state = await NewStateAsync();

        Assert.True(await state.SetEnabledAsync(false));

        Assert.Equal(CleanupIndicatorState.Off, state.State);
        Assert.Equal(1, _cleanup.UnloadCount);
    }

    [AvaloniaFact]
    public async Task SwitchedOn_ButTheModelIsGone_IsBroken()
    {
        await SwitchOnAsync();
        var state = await NewStateAsync();

        Assert.Equal(CleanupIndicatorState.Broken, state.State);
        Assert.True(state.IsWarning);
        Assert.Equal(Strings.Transcribe_Cleanup_Tooltip_Broken, state.Tooltip);
    }

    // --- Paused for the dictation language ---

    [AvaloniaFact]
    public async Task DictationLanguageNotEnglish_Pauses_AndTheSwitchStaysOn()
    {
        await InstallAsync();
        await SwitchOnAsync();
        var relationship = await RelationshipAsync(SpeechEngine.Whisper);
        var state = await NewStateAsync(relationship);
        Assert.Equal(CleanupIndicatorState.On, state.State);

        relationship.SelectSource("ru");

        Assert.Equal(CleanupIndicatorState.Paused, state.State);
        Assert.True(state.IsWarning);
        Assert.True(state.IsPausedForLanguage);
        Assert.True(state.IsEnabled);
        Assert.Equal(
            Strings.Format_Transcribe_Cleanup_Tooltip_PausedFormat(LanguageCatalog.GetDisplayLabel("ru")),
            state.Tooltip);
        Assert.Equal(
            Strings.Format_Settings_SpeechCleanup_PausedNoteFormat(LanguageCatalog.GetDisplayLabel("ru")),
            state.LanguageNote);

        relationship.SelectSource("en");

        Assert.Equal(CleanupIndicatorState.On, state.State);
        Assert.Equal(Strings.Settings_SpeechCleanup_LanguageNote, state.LanguageNote);
    }

    [AvaloniaFact]
    public async Task TranslatingToEnglish_Resumes()
    {
        await InstallAsync();
        await SwitchOnAsync();
        var relationship = await RelationshipAsync(SpeechEngine.Whisper);
        var state = await NewStateAsync(relationship);
        relationship.SelectSource("ru");
        Assert.Equal(CleanupIndicatorState.Paused, state.State);

        relationship.ToggleTranslation();

        Assert.Equal(CleanupIndicatorState.On, state.State);

        // Large v3 Turbo cannot translate (ADR-061): the text is Russian again.
        relationship.SetWhisperModel(WhisperModelType.LargeV3Turbo);

        Assert.Equal(CleanupIndicatorState.Paused, state.State);
    }

    [AvaloniaFact]
    public async Task Parakeet_IsNeverPausedByLanguagesLeftOverFromWhisper()
    {
        await InstallAsync();
        await SwitchOnAsync();
        await _settings.SetAsync(SettingsKeys.SelectedSourceLanguage, "ru", Ct);
        var relationship = await RelationshipAsync(SpeechEngine.Parakeet);

        var state = await NewStateAsync(relationship);

        Assert.Equal(CleanupIndicatorState.On, state.State);
    }

    // --- The verdict on the last dictation ---

    [AvaloniaTheory]
    [InlineData(SpeechCleanupOutcome.SkippedNotEnglish)]
    [InlineData(SpeechCleanupOutcome.SkippedTooLong)]
    [InlineData(SpeechCleanupOutcome.Failed)]
    public async Task DictationPastedAsSpoken_TurnsAmber_UntilTheNextRecording(SpeechCleanupOutcome outcome)
    {
        await InstallAsync();
        await SwitchOnAsync();
        var state = await NewStateAsync();

        state.ReportOutcome(outcome);

        Assert.Equal(CleanupIndicatorState.Skipped, state.State);
        Assert.True(state.IsWarning);
        Assert.NotEqual(Strings.Transcribe_Cleanup_Tooltip_On, state.Tooltip);

        state.ClearOutcome();

        Assert.Equal(CleanupIndicatorState.On, state.State);
    }

    [AvaloniaFact]
    public async Task CleanedDictation_StaysOn()
    {
        await InstallAsync();
        await SwitchOnAsync();
        var state = await NewStateAsync();

        state.ReportOutcome(SpeechCleanupOutcome.Cleaned);

        Assert.Equal(CleanupIndicatorState.On, state.State);
    }

    [AvaloniaFact]
    public async Task Tooltip_FollowsALanguageSwitch()
    {
        await InstallAsync();
        var state = await NewStateAsync();
        var english = state.Tooltip;
        var raised = false;
        state.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(state.Tooltip);

        Localizer.Instance.SetCulture(CultureInfo.GetCultureInfo("ru"));

        Assert.True(raised);
        Assert.NotEqual(english, state.Tooltip);
    }

    // --- The widget ---

    [AvaloniaFact]
    public async Task Widget_ClickTogglesCleanup_AndStopsARecordingInFlight()
    {
        await InstallAsync();
        var state = await NewStateAsync();
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(
            new MockWindowManager(), pipeline, new MockTextInjectionService(), cleanupState: state);
        Assert.True(vm.HasCleanupIndicator);
        await vm.StartRecordingAsync();

        await vm.ToggleCleanupCommand.ExecuteAsync(null);

        Assert.True(state.IsEnabled);
        Assert.False(vm.IsRecording);
        Assert.Equal(1, pipeline.StopCount);
    }

    [AvaloniaFact]
    public async Task Widget_NoIndicator_UntilCleanupIsSetUp()
    {
        var state = await NewStateAsync();
        var vm = new TranscribeViewModel(new MockWindowManager(), cleanupState: state);
        var raised = false;
        vm.PropertyChanged += (_, e) => raised |= e.PropertyName == nameof(vm.HasCleanupIndicator);
        Assert.False(vm.HasCleanupIndicator);

        await InstallAsync();
        await state.RefreshAsync();

        Assert.True(raised);
        Assert.True(vm.HasCleanupIndicator);
    }

    [AvaloniaFact]
    public async Task Widget_ClickWhileBroken_OpensTheSettingsPage()
    {
        await SwitchOnAsync();
        var state = await NewStateAsync();
        var windows = new MockWindowManager();
        var vm = new TranscribeViewModel(windows, cleanupState: state);

        await vm.ToggleCleanupCommand.ExecuteAsync(null);

        Assert.Equal(SettingsSection.SpeechCleanup, windows.LastSettingsSection);
        Assert.True(state.IsEnabled);
    }

    [AvaloniaFact]
    public async Task Widget_ReportsWhatCleanupDidWithEachDictation()
    {
        await InstallAsync();
        await SwitchOnAsync();
        var state = await NewStateAsync();
        var pipeline = new MockAudioPipeline();
        var vm = new TranscribeViewModel(
            new MockWindowManager(), pipeline, new MockTextInjectionService(), cleanupState: state);
        await vm.StartRecordingAsync();

        pipeline.RaiseTranscriptionAvailable("domani devo mandare il rapporto", SpeechCleanupOutcome.SkippedNotEnglish);
        Dispatcher.UIThread.RunJobs();

        Assert.Equal(CleanupIndicatorState.Skipped, state.State);

        // The next recording starts with a clean slate.
        await vm.StopRecordingAsync();
        await vm.StartRecordingAsync();

        Assert.Equal(CleanupIndicatorState.On, state.State);
    }

    // --- The settings page ---

    [AvaloniaFact]
    public async Task SettingsPage_FollowsASwitchFlippedOnTheWidget()
    {
        await InstallAsync();
        var state = await NewStateAsync();
        var page = new SpeechCleanupSettingsViewModel(
            _settings, _cleanup, _model, _registry, state: state);
        await page.Initialization;
        Assert.False(page.IsEnabled);

        await state.SetEnabledAsync(true);

        Assert.True(page.IsEnabled);

        await state.SetEnabledAsync(false);

        Assert.False(page.IsEnabled);
    }

    [AvaloniaFact]
    public async Task Widget_FollowsASwitchFlippedOnTheSettingsPage()
    {
        await InstallAsync();
        var state = await NewStateAsync();
        var page = new SpeechCleanupSettingsViewModel(
            _settings, _cleanup, _model, _registry, state: state);
        await page.Initialization;

        page.IsEnabled = true;
        await page.PendingToggle;

        Assert.Equal(CleanupIndicatorState.On, state.State);
    }

    [AvaloniaFact]
    public async Task SettingsPage_SaysWhyCleanupIsPaused()
    {
        await InstallAsync();
        await SwitchOnAsync();
        var relationship = await RelationshipAsync(SpeechEngine.Whisper);
        var state = await NewStateAsync(relationship);
        var page = new SpeechCleanupSettingsViewModel(
            _settings, _cleanup, _model, _registry, state: state);
        await page.Initialization;
        Assert.False(page.IsPausedForLanguage);
        Assert.Equal(Strings.Settings_SpeechCleanup_LanguageNote, page.LanguageNote);

        relationship.SelectSource("de");

        Assert.True(page.IsPausedForLanguage);
        Assert.Equal(
            Strings.Format_Settings_SpeechCleanup_PausedNoteFormat(LanguageCatalog.GetDisplayLabel("de")),
            page.LanguageNote);
    }
}
