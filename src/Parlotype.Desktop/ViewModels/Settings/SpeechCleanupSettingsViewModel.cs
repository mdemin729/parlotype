using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;
using Parlotype.Core.TextProcessing;
using Parlotype.Desktop.Resources;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// The Speech cleanup page (ADR-073): one switch, the model it needs, and how
/// the cleaned text should read.
/// </summary>
/// <remarks>
/// Turning the switch on is the whole setup. It fetches whatever is missing,
/// the model and, when none is installed, a <c>llama-server</c>, each behind its
/// own confirmation dialog, and only then saves the setting. If either step is
/// declined or fails the switch goes back to off, so the feature is never left
/// on in a state where it cannot work.
/// </remarks>
public partial class SpeechCleanupSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly ISpeechCleanupService? _cleanup;
    private readonly ISpeechCleanupModelInstaller? _modelInstaller;
    private readonly ILlamaServerRegistry? _registry;
    private readonly ILlamaServerCatalog? _catalog;
    private readonly ILlamaServerInstaller? _serverInstaller;
    private readonly TranscribeViewModel? _transcribeViewModel;
    private readonly ILogger<SpeechCleanupSettingsViewModel> _logger;

    /// <summary>
    /// Set while a property is being assigned from code rather than by the user:
    /// the initial load, and the switch snapping back after a failed setup.
    /// </summary>
    private bool _suppressChangeHandlers = true;

    /// <summary>
    /// Which status line is showing, retained so a language change can re-word
    /// it without touching the disk again (ADR-064).
    /// </summary>
    private StatusLabel _statusLabel = StatusLabel.NotSetUp;

    private enum StatusLabel
    {
        NotSetUp,
        Ready,
        Broken,
        SetupFailed,
    }

    public override string Title => Strings.Settings_SpeechCleanup_Title;
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;

    private static SpeechCleanupModelInfo Model => SpeechCleanupModelInfo.Default;

    /// <summary>
    /// "S1-mini by Superwhisper". Never translated or restyled: the model's
    /// licence requires this name wherever it is used.
    /// </summary>
    public string ModelName => Model.DisplayName;

    public string ModelSize => Model.DiskSize;

    public CleanupStylingDisplayItem[] StylingOptions { get; }

    [ObservableProperty]
    private bool _isEnabled;

    /// <summary>True while the switch is fetching the model or the server.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanChange))]
    private bool _isBusy;

    public bool CanChange => !IsBusy;

    [ObservableProperty]
    private string _statusText = string.Empty;

    /// <summary>
    /// The system's own error text behind a failed setup, shown as it arrived.
    /// Someone else's wording, so it is not re-translated on a language change.
    /// </summary>
    [ObservableProperty]
    private string? _errorDetail;

    [ObservableProperty]
    private bool _isModelInstalled;

    [ObservableProperty]
    private CleanupStyling _selectedStyling = SpeechCleanupOptions.Default.Styling;

    [ObservableProperty]
    private bool _listsEnabled;

    [ObservableProperty]
    private bool _emailLayoutEnabled;

    public SpeechCleanupSettingsViewModel(
        ISettingsService settings,
        ISpeechCleanupService? cleanup = null,
        ISpeechCleanupModelInstaller? modelInstaller = null,
        ILlamaServerRegistry? registry = null,
        ILlamaServerCatalog? catalog = null,
        ILlamaServerInstaller? serverInstaller = null,
        TranscribeViewModel? transcribeViewModel = null,
        ILogger<SpeechCleanupSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _cleanup = cleanup;
        _modelInstaller = modelInstaller;
        _registry = registry;
        _catalog = catalog;
        _serverInstaller = serverInstaller;
        _transcribeViewModel = transcribeViewModel;
        _logger = logger ?? NullLogger<SpeechCleanupSettingsViewModel>.Instance;

        StylingOptions = Enum.GetValues<CleanupStyling>()
            .Select(s => new CleanupStylingDisplayItem(s, SelectStylingCommand))
            .ToArray();
        MarkSelectedStyling();
        StatusText = Describe(_statusLabel);

        Initialization = InitializeAsync();
    }

    /// <summary>Completes when the saved settings have been applied. For tests.</summary>
    internal Task Initialization { get; }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public SpeechCleanupSettingsViewModel() : this(new DesignSettingsService()) { }

    private async Task InitializeAsync()
    {
        var saved = await SpeechCleanupSettings.LoadAsync(_settings);

        _suppressChangeHandlers = true;
        try
        {
            IsEnabled = saved.Enabled;
            SelectedStyling = saved.Options.Styling;
            ListsEnabled = saved.Options.Structure == CleanupStructure.Lists;
            EmailLayoutEnabled = saved.Options.Context == CleanupContext.Email;
            MarkSelectedStyling();
        }
        finally
        {
            _suppressChangeHandlers = false;
        }

        await RefreshAsync();
    }

    /// <summary>
    /// Re-reads what is on disk and updates the status line. Called when the page
    /// is opened, because the model or the server can be removed from elsewhere
    /// (the llama.cpp server page, the Data page) while this one is not looking.
    /// </summary>
    public async Task RefreshAsync()
    {
        IsModelInstalled = _modelInstaller?.IsInstalled(Model) ?? false;

        // A failed setup stays on screen until the user acts again; a routine
        // refresh must not wipe the explanation of why the switch went back off.
        if (IsBusy || _statusLabel == StatusLabel.SetupFailed)
            return;

        SetStatus(await ReadStatusAsync());
    }

    private async Task<StatusLabel> ReadStatusAsync()
    {
        var readiness = _cleanup is null
            ? SpeechCleanupReadiness.ModelMissing
            : await _cleanup.GetReadinessAsync();

        if (readiness == SpeechCleanupReadiness.Ready)
            return StatusLabel.Ready;

        return IsEnabled ? StatusLabel.Broken : StatusLabel.NotSetUp;
    }

    partial void OnIsEnabledChanged(bool value)
    {
        if (_suppressChangeHandlers)
            return;

        _ = value ? EnableAsync() : DisableAsync();
    }

    /// <summary>The last enable or disable started from the switch. For tests.</summary>
    internal Task PendingToggle { get; private set; } = Task.CompletedTask;

    private Task EnableAsync() => PendingToggle = EnableCoreAsync();

    private Task DisableAsync() => PendingToggle = DisableCoreAsync();

    private async Task EnableCoreAsync()
    {
        IsBusy = true;
        ErrorDetail = null;
        try
        {
            if (!await EnsureModelAsync() || !await EnsureServerAsync())
            {
                // Declined, not failed: nothing to explain.
                RevertSwitch();
                SetStatus(StatusLabel.NotSetUp);
                return;
            }

            await _settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, bool.TrueString);
            _logger.LogInformation("Speech cleanup enabled");
            SetStatus(StatusLabel.Ready);
            await StopRecordingAsync();
        }
        catch (OperationCanceledException)
        {
            RevertSwitch();
            SetStatus(StatusLabel.NotSetUp);
        }
        catch (NoServerBuildException)
        {
            // Our own finding, not the system's: there is no outside wording to
            // show, and the status line already says setup did not finish.
            _logger.LogError("Speech cleanup setup failed: the llama.cpp catalog has no CPU build");
            RevertSwitch();
            SetStatus(StatusLabel.SetupFailed);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Speech cleanup setup failed");
            RevertSwitch();
            ErrorDetail = ex.Message;
            SetStatus(StatusLabel.SetupFailed);
        }
        finally
        {
            IsModelInstalled = _modelInstaller?.IsInstalled(Model) ?? false;
            IsBusy = false;
        }
    }

    private async Task DisableCoreAsync()
    {
        await _settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, bool.FalseString);
        _logger.LogInformation("Speech cleanup disabled");
        ErrorDetail = null;

        // Free the model's memory now rather than at exit.
        if (_cleanup is not null)
            await _cleanup.UnloadAsync();

        SetStatus(await ReadStatusAsync());
        await StopRecordingAsync();
    }

    private async Task<bool> EnsureModelAsync()
    {
        if (_modelInstaller is null)
            return false;

        return await _modelInstaller.TryInstallAsync(Model);
    }

    /// <summary>
    /// Makes sure an installed <c>llama-server</c> is selected, without sending
    /// the user to the llama.cpp server page.
    /// </summary>
    /// <remarks>
    /// In order: keep the active install if it works; otherwise select a managed
    /// install that is already on disk; otherwise install the newest CPU build
    /// (see <see cref="LlamaServerVariantPicker"/> for why CPU). Selecting an
    /// install changes the one the Gemma 4 engine uses too, but only ever away
    /// from a selection whose executable does not exist.
    /// </remarks>
    private async Task<bool> EnsureServerAsync()
    {
        if (_cleanup is null || _registry is null)
            return false;

        if (await _cleanup.GetReadinessAsync() == SpeechCleanupReadiness.Ready)
            return true;

        var install = (await _registry.ListManagedAsync()).FirstOrDefault(i => i.IsValid);
        if (install is null)
        {
            if (_catalog is null || _serverInstaller is null)
                return false;

            var variant = LlamaServerVariantPicker.PickCpuVariant(
                await _catalog.FetchAsync(forceRefresh: false));
            if (variant is null)
                throw new NoServerBuildException();

            // Shows its own confirm-and-progress dialog; declining throws
            // OperationCanceledException, which the caller treats as "no".
            install = await _serverInstaller.InstallAsync(variant, progress: null);
        }

        await _registry.SetActiveAsync(install.Id, LlamaServerSource.Managed);
        _logger.LogInformation("Speech cleanup selected llama-server install {Id}", install.Id);

        return await _cleanup.GetReadinessAsync() == SpeechCleanupReadiness.Ready;
    }

    [RelayCommand]
    private async Task DownloadModelAsync()
    {
        if (_modelInstaller is null)
            return;

        await _modelInstaller.TryInstallAsync(Model);
        await RefreshAfterModelChangeAsync();
    }

    [RelayCommand]
    private async Task DeleteModelAsync()
    {
        if (_modelInstaller is null)
            return;

        // The server holds the file open; and without the model the feature
        // cannot stay on.
        if (_cleanup is not null)
            await _cleanup.UnloadAsync();

        await _modelInstaller.DeleteAsync(Model);
        _logger.LogInformation("Speech cleanup model deleted");

        if (IsEnabled)
        {
            RevertSwitch();
            await _settings.SetAsync(SettingsKeys.SpeechCleanupEnabled, bool.FalseString);
            await StopRecordingAsync();
        }

        await RefreshAfterModelChangeAsync();
    }

    private async Task RefreshAfterModelChangeAsync()
    {
        ErrorDetail = null;
        IsModelInstalled = _modelInstaller?.IsInstalled(Model) ?? false;
        SetStatus(await ReadStatusAsync());
    }

    [RelayCommand]
    private void SelectStyling(CleanupStyling styling) => SelectedStyling = styling;

    partial void OnSelectedStylingChanged(CleanupStyling value)
    {
        MarkSelectedStyling();
        SaveOption(SettingsKeys.SpeechCleanupStyling, value.ToString());
    }

    partial void OnListsEnabledChanged(bool value) =>
        SaveOption(
            SettingsKeys.SpeechCleanupStructure,
            (value ? CleanupStructure.Lists : CleanupStructure.Prose).ToString());

    partial void OnEmailLayoutEnabledChanged(bool value) =>
        SaveOption(
            SettingsKeys.SpeechCleanupContext,
            (value ? CleanupContext.Email : CleanupContext.General).ToString());

    private void SaveOption(string key, string value)
    {
        if (_suppressChangeHandlers)
            return;

        _logger.LogInformation("Speech cleanup option {Key}: {Value}", key, value);
        _ = SaveAndStopAsync(key, value);
    }

    private async Task SaveAndStopAsync(string key, string value)
    {
        await _settings.SetAsync(key, value);
        await StopRecordingAsync();
    }

    /// <summary>
    /// The pipeline snapshots these settings when a recording starts, so one in
    /// flight would finish under the old ones. Same rule as every other page.
    /// </summary>
    private async Task StopRecordingAsync()
    {
        if (_transcribeViewModel is { IsRecording: true })
        {
            _logger.LogInformation("Stopping recording after a speech cleanup change");
            await _transcribeViewModel.StopRecordingAsync();
        }
    }

    private void RevertSwitch()
    {
        _suppressChangeHandlers = true;
        try
        {
            IsEnabled = false;
        }
        finally
        {
            _suppressChangeHandlers = false;
        }
    }

    private void MarkSelectedStyling()
    {
        foreach (var item in StylingOptions)
            item.IsSelected = item.Styling == SelectedStyling;
    }

    private void SetStatus(StatusLabel label)
    {
        _statusLabel = label;
        StatusText = Describe(label);
    }

    private string Describe(StatusLabel label) => label switch
    {
        StatusLabel.Ready => Strings.Settings_SpeechCleanup_Status_Ready,
        StatusLabel.Broken => Strings.Settings_SpeechCleanup_Status_Broken,
        StatusLabel.SetupFailed => Strings.Settings_SpeechCleanup_Status_SetupFailed,
        _ => Strings.Format_Settings_SpeechCleanup_Status_NotSetUpFormat(ModelSize),
    };

    /// <summary>
    /// Re-words the retained status and the style cards (ADR-064). Reads nothing
    /// from disk: a language change is not a reason to.
    /// </summary>
    protected override void OnCultureChanged()
    {
        base.OnCultureChanged();
        StatusText = Describe(_statusLabel);
        foreach (var item in StylingOptions)
            item.RefreshText();
    }

    /// <summary>The release list offers no CPU build for this OS and architecture.</summary>
    private sealed class NoServerBuildException : Exception;

    private sealed class DesignSettingsService : ISettingsService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
