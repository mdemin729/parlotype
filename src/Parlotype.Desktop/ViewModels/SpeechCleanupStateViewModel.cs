using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels;

/// <summary>What the speech cleanup indicator on the Transcribe widget shows.</summary>
public enum CleanupIndicatorState
{
    /// <summary>Cleanup was never set up: nothing is shown, and the widget is unchanged.</summary>
    Hidden,

    /// <summary>Set up and switched off. One click turns it on.</summary>
    Off,

    /// <summary>Switched on and able to run.</summary>
    On,

    /// <summary>Switched on, but the dictation language is not English, so it will not run.</summary>
    Paused,

    /// <summary>Switched on, but the last dictation was pasted as spoken.</summary>
    Skipped,

    /// <summary>Switched on, but the model or the server has gone missing.</summary>
    Broken,
}

/// <summary>
/// Whether speech cleanup will run on the next dictation and why not, shared by
/// the Speech cleanup settings page and the Transcribe widget so the two never
/// disagree (ADR-075). Same role <see cref="LanguageRelationshipViewModel"/>
/// plays for languages, and it reads its language inputs from that one.
/// </summary>
/// <remarks>
/// The output language comes from <see cref="CleanupLanguagePolicy"/>, the
/// function the audio pipeline itself uses, so "paused" here means the pipeline
/// will not call the model. The stored switch is never changed by a pause: the
/// user's choice stays on and takes effect again when the language allows it.
/// </remarks>
public sealed partial class SpeechCleanupStateViewModel : ObservableObject
{
    private readonly ISettingsService _settings;
    private readonly ISpeechCleanupService? _cleanup;
    private readonly LanguageRelationshipViewModel? _relationship;
    private readonly ILogger<SpeechCleanupStateViewModel> _logger;

    /// <summary>The stored switch (<see cref="SettingsKeys.SpeechCleanupEnabled"/>).</summary>
    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>Whether the model and a server are on disk.</summary>
    [ObservableProperty]
    private SpeechCleanupReadiness _readiness = SpeechCleanupReadiness.ModelMissing;

    /// <summary>The language the next dictation will be pasted in, as far as the settings tell.</summary>
    [ObservableProperty]
    private CleanupLanguage _language = CleanupLanguage.Unknown;

    /// <summary>What cleanup did with the most recent dictation of the current recording.</summary>
    [ObservableProperty]
    private SpeechCleanupOutcome _lastOutcome = SpeechCleanupOutcome.NotRun;

    public SpeechCleanupStateViewModel(
        ISettingsService settings,
        ISpeechCleanupService? cleanup = null,
        LanguageRelationshipViewModel? relationship = null,
        ILogger<SpeechCleanupStateViewModel>? logger = null)
    {
        _settings = settings;
        _cleanup = cleanup;
        _relationship = relationship;
        _logger = logger ?? NullLogger<SpeechCleanupStateViewModel>.Instance;

        // Not a settings section, so there is no OnCultureChanged hook: the
        // tooltip and the note are composed here and must be re-raised by hand.
        Localizer.Instance.CultureChanged += (_, _) => NotifyText();

        if (_relationship is not null)
            _relationship.PropertyChanged += (_, _) => Language = ResolveLanguage();

        Initialization = InitializeAsync();
    }

    /// <summary>Completes when the stored state has been read. For hosts and tests.</summary>
    public Task Initialization { get; }

    private async Task InitializeAsync()
    {
        try
        {
            if (_relationship is not null)
                await _relationship.InitializeAsync();

            Language = ResolveLanguage();
            await RefreshAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speech cleanup state initialization failed");
        }
    }

    // ----- Derived state -------------------------------------------------

    public CleanupIndicatorState State
    {
        get
        {
            var ready = Readiness == SpeechCleanupReadiness.Ready;
            if (!IsEnabled)
                return ready ? CleanupIndicatorState.Off : CleanupIndicatorState.Hidden;

            if (!ready)
                return CleanupIndicatorState.Broken;

            if (Language.Kind == CleanupLanguageKind.NotEnglish)
                return CleanupIndicatorState.Paused;

            return LastOutcome is SpeechCleanupOutcome.SkippedNotEnglish
                or SpeechCleanupOutcome.SkippedTooLong
                or SpeechCleanupOutcome.Failed
                ? CleanupIndicatorState.Skipped
                : CleanupIndicatorState.On;
        }
    }

    /// <summary>False until cleanup has been set up once: the widget then shows nothing new.</summary>
    public bool IsIndicatorVisible => State != CleanupIndicatorState.Hidden;

    public bool IsOn => State == CleanupIndicatorState.On;

    public bool IsOff => State == CleanupIndicatorState.Off;

    /// <summary>Amber: switched on, but the next or the last dictation is not cleaned.</summary>
    public bool IsWarning => State
        is CleanupIndicatorState.Paused or CleanupIndicatorState.Skipped or CleanupIndicatorState.Broken;

    /// <summary>Switched on, and held back only by the dictation language.</summary>
    public bool IsPausedForLanguage => IsEnabled && Language.Kind == CleanupLanguageKind.NotEnglish;

    /// <summary>The indicator's tooltip: the state, and what a click does.</summary>
    public string Tooltip => State switch
    {
        CleanupIndicatorState.Off => Strings.Transcribe_Cleanup_Tooltip_Off,
        CleanupIndicatorState.Paused => Strings.Format_Transcribe_Cleanup_Tooltip_PausedFormat(LanguageName),
        CleanupIndicatorState.Broken => Strings.Transcribe_Cleanup_Tooltip_Broken,
        CleanupIndicatorState.Skipped => LastOutcome switch
        {
            SpeechCleanupOutcome.SkippedTooLong => Strings.Transcribe_Cleanup_Tooltip_SkippedTooLong,
            SpeechCleanupOutcome.Failed => Strings.Transcribe_Cleanup_Tooltip_Failed,
            _ => Strings.Transcribe_Cleanup_Tooltip_SkippedNotEnglish,
        },
        _ => Strings.Transcribe_Cleanup_Tooltip_On,
    };

    /// <summary>The line under the switch on the settings page.</summary>
    public string LanguageNote => IsPausedForLanguage
        ? Strings.Format_Settings_SpeechCleanup_PausedNoteFormat(LanguageName)
        : Strings.Settings_SpeechCleanup_LanguageNote;

    private string LanguageName => LanguageCatalog.GetDisplayLabel(Language.Code);

    // ----- Changes -------------------------------------------------------

    /// <summary>
    /// Re-reads the stored switch and what is on disk. Called after anything
    /// that may have changed either: the settings page, a model deletion, the
    /// start of a recording.
    /// </summary>
    public async Task RefreshAsync()
    {
        var saved = await SpeechCleanupSettings.LoadAsync(_settings);
        var readiness = _cleanup is null
            ? SpeechCleanupReadiness.ModelMissing
            : await _cleanup.GetReadinessAsync();

        IsEnabled = saved.Enabled;
        Readiness = readiness;
    }

    /// <summary>
    /// Flips the stored switch from the widget. Turning it on is refused unless
    /// everything it needs is installed: the downloads belong to the settings
    /// page, which asks first (ADR-073). Returns whether the switch changed.
    /// </summary>
    public async Task<bool> SetEnabledAsync(bool enabled)
    {
        await RefreshAsync();
        if (IsEnabled == enabled)
            return false;

        if (enabled && Readiness != SpeechCleanupReadiness.Ready)
            return false;

        await _settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, enabled.ToString());
        _logger.LogInformation("Speech cleanup {State} from the Transcribe widget", enabled ? "enabled" : "disabled");

        // Free the model's memory now rather than at exit, as the page does.
        if (!enabled && _cleanup is not null)
            await _cleanup.UnloadAsync();

        LastOutcome = SpeechCleanupOutcome.NotRun;
        IsEnabled = enabled;
        return true;
    }

    /// <summary>Records what cleanup did with a dictation. UI thread.</summary>
    public void ReportOutcome(SpeechCleanupOutcome outcome) => LastOutcome = outcome;

    /// <summary>A new recording starts with no verdict on a previous one. UI thread.</summary>
    public void ClearOutcome() => LastOutcome = SpeechCleanupOutcome.NotRun;

    private CleanupLanguage ResolveLanguage() => _relationship is null
        ? CleanupLanguage.Unknown
        : CleanupLanguagePolicy.Resolve(
            _relationship.Engine,
            _relationship.WhisperModel,
            _relationship.SourceCode,
            _relationship.DetectedKeyboardLayout,
            _relationship.TranslationEnabled,
            _relationship.TargetCode);

    partial void OnIsEnabledChanged(bool value) => NotifyState();

    partial void OnReadinessChanged(SpeechCleanupReadiness value) => NotifyState();

    partial void OnLanguageChanged(CleanupLanguage value) => NotifyState();

    partial void OnLastOutcomeChanged(SpeechCleanupOutcome value) => NotifyState();

    private void NotifyState()
    {
        OnPropertyChanged(nameof(State));
        OnPropertyChanged(nameof(IsIndicatorVisible));
        OnPropertyChanged(nameof(IsOn));
        OnPropertyChanged(nameof(IsOff));
        OnPropertyChanged(nameof(IsWarning));
        OnPropertyChanged(nameof(IsPausedForLanguage));
        NotifyText();
    }

    private void NotifyText()
    {
        OnPropertyChanged(nameof(Tooltip));
        OnPropertyChanged(nameof(LanguageNote));
    }
}
