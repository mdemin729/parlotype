using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.LlamaServer;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Desktop.Services;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Shows where Parlotype keeps user data and how much space it uses, and owns the
/// two destructive controls: reclaim disk space now, and delete everything on
/// uninstall (ADR-053).
/// </summary>
/// <remarks>
/// The uninstall toggle exists because Velopack's uninstall hook may not show UI
/// and so cannot ask. Consent is captured here, while a real dialog can be shown;
/// the hook in <see cref="Program"/> only executes the recorded decision.
/// </remarks>
public partial class DataSettingsViewModel : SettingsSectionViewModelBase
{
    private readonly ISettingsService _settings;
    private readonly IAppPaths _paths;
    private readonly IUserDialogService? _dialogs;
    private readonly IShellService? _shell;
    private readonly ISpeechRecognizer? _recognizer;
    private readonly ILlamaCppServerLifecycle? _serverLifecycle;
    private readonly ILogger<DataSettingsViewModel> _logger;

    /// <summary>Guards against the initial settings load echoing back as a user edit.</summary>
    private bool _loading = true;

    public override string Title => "Data";
    public override SettingsCategory Category => SettingsCategory.Application;

    [ObservableProperty]
    private string _dataDirectory = string.Empty;

    [ObservableProperty]
    private string _modelsSizeText = "—";

    /// <summary>Transient feedback for the copy/open buttons. Null hides the line.</summary>
    [ObservableProperty]
    private string? _pathActionMessage;

    [ObservableProperty]
    private bool _uninstallRemovesData;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private string? _statusMessage;

    public DataSettingsViewModel(
        ISettingsService settings,
        IAppPaths? paths = null,
        IUserDialogService? dialogs = null,
        IShellService? shell = null,
        ISpeechRecognizer? recognizer = null,
        ILlamaCppServerLifecycle? serverLifecycle = null,
        ILogger<DataSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _paths = paths ?? AppPaths.Default;
        _dialogs = dialogs;
        _shell = shell;
        _recognizer = recognizer;
        _serverLifecycle = serverLifecycle;
        _logger = logger ?? NullLogger<DataSettingsViewModel>.Instance;

        DataDirectory = _paths.DataDirectory;

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        var saved = await _settings.GetAsync<string>(SettingsKeys.UninstallRemovesUserData);
        UninstallRemovesData = bool.TryParse(saved, out var remove) && remove;
        _loading = false;

        await RefreshSizeAsync();
    }

    /// <summary>
    /// Completes when the consent write has reached disk. Awaited on shutdown so the
    /// process cannot exit while the flag still disagrees with what the user chose.
    /// </summary>
    public Task PendingWrite { get; private set; } = Task.CompletedTask;

    partial void OnUninstallRemovesDataChanged(bool value)
    {
        if (_loading)
            return;

        PendingWrite = PersistConsentAsync(value);
    }

    /// <summary>
    /// Writes the consent flag, confirms it landed, and reverts the toggle if it
    /// did not.
    /// </summary>
    /// <remarks>
    /// Unlike the other settings pages, this one cannot fire-and-forget. The flag is
    /// read later by the uninstall hook — a process with no UI, no chance to ask, and
    /// the ability to delete several GB of models. A toggle showing "keep my data"
    /// while settings.json still says "delete everything" is silent data loss, so the
    /// displayed state is only ever what the write actually achieved.
    /// </remarks>
    private async Task PersistConsentAsync(bool value)
    {
        IsBusy = true;
        StatusMessage = null;
        try
        {
            await _settings.SetAsync(SettingsKeys.UninstallRemovesUserData, value.ToString());

            // Read back rather than trusting the write: this is the gate on a
            // destructive action, and the cost of confirming it is one dictionary hit.
            var stored = await _settings.GetAsync<string>(SettingsKeys.UninstallRemovesUserData);
            if (!bool.TryParse(stored, out var persisted) || persisted != value)
                throw new InvalidOperationException(
                    $"settings.json still reads '{stored ?? "(absent)"}'");

            _logger.LogInformation("Delete user data on uninstall: {Enabled}", value);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to save the uninstall data-removal preference");

            RevertTo(!value);
            StatusMessage = value
                ? "Could not save that preference. Uninstalling will still keep your data."
                : "Could not save that preference — uninstalling will still delete your "
                  + "downloaded models, settings and API keys. Please try again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>Puts the toggle back without treating the correction as a new edit.</summary>
    private void RevertTo(bool value)
    {
        _loading = true;
        UninstallRemovesData = value;
        _loading = false;
    }

    [RelayCommand]
    private async Task RefreshSizeAsync()
    {
        var bytes = await Task.Run(() => MeasureBytes(_paths.ModelsDirectory));
        ModelsSizeText = Describe(bytes);
    }

    [RelayCommand]
    private async Task CopyPathAsync()
    {
        if (_shell is null)
            return;

        PathActionMessage = await _shell.CopyTextAsync(_paths.DataDirectory)
            ? "Path copied to the clipboard."
            : "Could not copy the path.";
    }

    [RelayCommand]
    private async Task OpenFolderAsync()
    {
        if (_shell is null)
            return;

        PathActionMessage = await _shell.OpenDirectoryAsync(_paths.DataDirectory)
            ? null
            : "Could not open the folder.";
    }

    /// <summary>
    /// Deletes downloaded speech models after confirmation. Settings and API keys
    /// are left alone — this is a "reclaim disk space" action, not a reset.
    /// </summary>
    [RelayCommand]
    private async Task DeleteModelsAsync()
    {
        if (_dialogs is null)
            return;

        var confirmed = await _dialogs.ShowConfirmationAsync(
            "Delete downloaded models?",
            $"This removes {ModelsSizeText} from {_paths.ModelsDirectory}.\n\n"
            + "Your settings and API keys are kept. Models download again automatically "
            + "the next time you dictate.",
            "Delete models",
            "Cancel");

        if (!confirmed)
            return;

        IsBusy = true;
        StatusMessage = null;
        try
        {
            // Windows locks a loaded model file. Release both holders first — the
            // in-process recognizer and the llama-server sidecar — the same order
            // the llama.cpp installer uses before replacing its files.
            if (_recognizer is not null)
                await _recognizer.UnloadAsync();

            if (_serverLifecycle is not null)
                await _serverLifecycle.StopForReplacementAsync();

            if (Directory.Exists(_paths.ModelsDirectory))
                await Task.Run(() => Directory.Delete(_paths.ModelsDirectory, recursive: true));

            _logger.LogInformation("Deleted downloaded models from {Directory}", _paths.ModelsDirectory);
            StatusMessage = "Downloaded models deleted.";
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete downloaded models");
            StatusMessage = "Could not delete every file — something is still using it. "
                + "Restart Parlotype and try again.";
        }
        finally
        {
            IsBusy = false;
            await RefreshSizeAsync();
        }
    }

    private static long MeasureBytes(string directory)
    {
        try
        {
            var info = new DirectoryInfo(directory);
            return info.Exists
                ? info.EnumerateFiles("*", SearchOption.AllDirectories).Sum(f => f.Length)
                : 0L;
        }
        catch (Exception)
        {
            // Size is decoration; an unreadable directory must not break the page.
            return 0L;
        }
    }

    private static string Describe(long bytes) => bytes switch
    {
        <= 0 => "Nothing downloaded",
        < 1024L * 1024 => $"{bytes / 1024d:F0} KB",
        < 1024L * 1024 * 1024 => $"{bytes / 1024d / 1024d:F0} MB",
        _ => $"{bytes / 1024d / 1024d / 1024d:F1} GB",
    };
}
