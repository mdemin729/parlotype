using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Core.Speech.LlamaServer;
using Parlotype.Platform.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class LlamaCppSettingsViewModel : SettingsSectionViewModelBase
{
    private const int DefaultPort = 8321;
    private const string DefaultHost = "127.0.0.1";

    private static string DefaultServerFolder =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "llama-server");

    private readonly ISettingsService _settings;
    private readonly ISpeechRecognizer? _recognizer;
    private readonly ILlamaServerRegistry? _registry;
    private readonly ILlamaServerCatalog? _catalog;
    private readonly ILlamaServerInstaller? _installer;
    private readonly ILogger<LlamaCppSettingsViewModel> _logger;

    public override string Title => "llama.cpp";

    // --- Server status (probe results) ---

    [ObservableProperty]
    private string _statusText = "Not probed";

    [ObservableProperty]
    private string _statusColor = "Gray";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private bool _hasPortConflict;

    [ObservableProperty]
    private string? _errorMessage;

    // --- Server properties (from /props) ---

    [ObservableProperty]
    private string? _modelAlias;

    [ObservableProperty]
    private string? _modelPath;

    [ObservableProperty]
    private bool _audioSupported;

    [ObservableProperty]
    private string? _buildInfo;

    // --- User-editable settings (legacy manual mode + port) ---

    [ObservableProperty]
    private string _portText = DefaultPort.ToString();

    [ObservableProperty]
    private string _serverFolder = "";

    [ObservableProperty]
    private bool _isRefreshing;

    // --- Managed install state ---

    public ObservableCollection<LlamaServerInstallRowVm> Installed { get; } = new();
    public ObservableCollection<LlamaServerVariantRowVm> Available { get; } = new();

    [ObservableProperty]
    private bool _hasManagedFeatures;

    [ObservableProperty]
    private bool _isManagedActive;

    [ObservableProperty]
    private bool _isManualActive;

    [ObservableProperty]
    private LlamaServerInstallRowVm? _activeManagedInstall;

    [ObservableProperty]
    private string? _manualFolderPath;

    [ObservableProperty]
    private bool _isLoadingCatalog;

    [ObservableProperty]
    private bool _isCheckingForUpdates;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string? _latestAvailableBuild;

    [ObservableProperty]
    private bool _isInstallInProgress;

    [ObservableProperty]
    private bool _isUninstallInProgress;

    [ObservableProperty]
    private string? _catalogErrorMessage;

    public LlamaCppSettingsViewModel(
        ISettingsService settings,
        ISpeechRecognizer? recognizer = null,
        ILlamaServerRegistry? registry = null,
        ILlamaServerCatalog? catalog = null,
        ILlamaServerInstaller? installer = null,
        ILogger<LlamaCppSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _recognizer = recognizer;
        _registry = registry;
        _catalog = catalog;
        _installer = installer;
        _logger = logger ?? NullLogger<LlamaCppSettingsViewModel>.Instance;
        _hasManagedFeatures = registry is not null && installer is not null;

        _ = InitializeAsync();
    }

    /// <summary>Parameterless constructor for designer support.</summary>
    public LlamaCppSettingsViewModel() : this(new DesignSettingsService()) { }

    private async Task InitializeAsync()
    {
        var portStr = await _settings.GetAsync<string>(SettingsKeys.LlamaCppPort);
        if (int.TryParse(portStr, out var port) && port is > 0 and <= 65535)
            PortText = port.ToString();

        var folder = await _settings.GetAsync<string>(SettingsKeys.LlamaCppServerFolder);
        ServerFolder = folder ?? DefaultServerFolder;
        ManualFolderPath = folder;

        if (HasManagedFeatures)
        {
            await RefreshInstalledAsync();
            await LoadCatalogAsync(forceRefresh: false);
        }
    }

    [RelayCommand]
    private async Task RefreshServerInfoAsync()
    {
        IsRefreshing = true;
        ErrorMessage = null;

        try
        {
            if (!int.TryParse(PortText, out var port) || port is <= 0 or > 65535)
            {
                StatusText = "Invalid port";
                StatusColor = "Red";
                ErrorMessage = "Port must be a number between 1 and 65535.";
                IsConnected = false;
                HasPortConflict = false;
                return;
            }

            var info = await LlamaCppServerInfo.ProbeAsync(DefaultHost, port);

            switch (info.Status)
            {
                case LlamaCppServerStatus.Connected:
                    StatusText = "Connected";
                    StatusColor = "Green";
                    IsConnected = true;
                    HasPortConflict = false;
                    ModelAlias = info.ModelAlias;
                    ModelPath = info.ModelPath;
                    AudioSupported = info.AudioSupported;
                    BuildInfo = info.BuildInfo;
                    break;

                case LlamaCppServerStatus.Disconnected:
                    StatusText = "Disconnected";
                    StatusColor = "Gray";
                    IsConnected = false;
                    HasPortConflict = false;
                    ClearServerProps();
                    break;

                case LlamaCppServerStatus.PortConflict:
                    StatusText = "Port conflict";
                    StatusColor = "Orange";
                    IsConnected = false;
                    HasPortConflict = true;
                    ErrorMessage = info.ErrorMessage;
                    ClearServerProps();
                    break;

                case LlamaCppServerStatus.Loading:
                    StatusText = "Loading model...";
                    StatusColor = "Blue";
                    IsConnected = false;
                    HasPortConflict = false;
                    ClearServerProps();
                    break;

                default:
                    StatusText = "Error";
                    StatusColor = "Red";
                    IsConnected = false;
                    HasPortConflict = false;
                    ErrorMessage = info.ErrorMessage;
                    ClearServerProps();
                    break;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to probe llama-server");
            StatusText = "Error";
            StatusColor = "Red";
            ErrorMessage = ex.Message;
            IsConnected = false;
            HasPortConflict = false;
            ClearServerProps();
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    [RelayCommand]
    private async Task SaveSettingsAsync()
    {
        if (!int.TryParse(PortText, out var port) || port is <= 0 or > 65535)
        {
            ErrorMessage = "Port must be a number between 1 and 65535.";
            return;
        }

        var folder = ServerFolder?.Trim();
        if (string.IsNullOrEmpty(folder))
        {
            ErrorMessage = "Server folder cannot be empty.";
            return;
        }

        await _settings.SetAsync(SettingsKeys.LlamaCppPort, port.ToString());
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, folder);
        _logger.LogInformation("llama.cpp settings saved: port={Port}, folder={Folder}", port, folder);
        ErrorMessage = null;
        ManualFolderPath = folder;

        await UnloadRecognizerAsync();
        await RefreshServerInfoAsync();
    }

    [RelayCommand]
    private async Task ResetDefaultsAsync()
    {
        PortText = DefaultPort.ToString();
        ServerFolder = DefaultServerFolder;

        await _settings.SetAsync(SettingsKeys.LlamaCppPort, DefaultPort.ToString());
        await _settings.SetAsync(SettingsKeys.LlamaCppServerFolder, DefaultServerFolder);
        _logger.LogInformation("llama.cpp settings reset to defaults");
        ErrorMessage = null;
        ManualFolderPath = DefaultServerFolder;

        await UnloadRecognizerAsync();
        await RefreshServerInfoAsync();
    }

    [RelayCommand]
    private async Task BrowseServerFolderAsync()
    {
        var topLevel = Avalonia.Application.Current?.ApplicationLifetime
            is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop
            ? desktop.Windows.FirstOrDefault()
            : null;

        if (topLevel is null)
            return;

        var result = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "Select llama-server folder",
                AllowMultiple = false,
            });

        if (result is { Count: > 0 })
        {
            ServerFolder = result[0].Path.LocalPath;
        }
    }

    // --- Managed install commands ---

    [RelayCommand]
    private Task RefreshInstalledAsync() => ReloadInstalledAndActiveAsync();

    [RelayCommand]
    private async Task LoadCatalogAsync(bool forceRefresh)
    {
        if (_catalog is null) return;

        IsLoadingCatalog = true;
        CatalogErrorMessage = null;
        try
        {
            var groups = await _catalog.FetchAsync(forceRefresh);
            await PopulateAvailableAsync(groups);
            UpdateUpdateBanner(groups);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load llama.cpp catalog");
            CatalogErrorMessage = ex.Message;
        }
        finally
        {
            IsLoadingCatalog = false;
        }
    }

    [RelayCommand]
    private async Task CheckForUpdatesAsync()
    {
        if (_catalog is null) return;
        IsCheckingForUpdates = true;
        try
        {
            await LoadCatalogAsync(forceRefresh: true);
        }
        finally
        {
            IsCheckingForUpdates = false;
        }
    }

    [RelayCommand]
    private async Task InstallVariantAsync(LlamaServerVariantRowVm? row)
    {
        if (row is null || _installer is null) return;

        IsInstallInProgress = true;
        CatalogErrorMessage = null;
        try
        {
            await _installer.InstallAsync(row.Variant, progress: null);
            await ReloadInstalledAndActiveAsync();
            // Reflect the newly-installed item in the Available list (badge it as installed).
            await RecomputeAvailableInstalledFlagsAsync();
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("Install of {Asset} cancelled", row.AssetName);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Install of {Asset} failed", row.AssetName);
            CatalogErrorMessage = ex.Message;
        }
        finally
        {
            IsInstallInProgress = false;
        }
    }

    [RelayCommand]
    private async Task UninstallInstallAsync(LlamaServerInstallRowVm? row)
    {
        if (row is null || _installer is null) return;

        IsUninstallInProgress = true;
        try
        {
            await _installer.UninstallAsync(row.Id);
            await ReloadInstalledAndActiveAsync();
            await RecomputeAvailableInstalledFlagsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uninstall of {Id} failed", row.Id);
            CatalogErrorMessage = ex.Message;
        }
        finally
        {
            IsUninstallInProgress = false;
        }
    }

    [RelayCommand]
    private async Task SetActiveManagedAsync(LlamaServerInstallRowVm? row)
    {
        if (row is null || _registry is null) return;
        await _registry.SetActiveAsync(row.Id, LlamaServerSource.Managed);
        await UnloadRecognizerAsync();
        await ReloadInstalledAndActiveAsync();
    }

    [RelayCommand]
    private async Task SetActiveManualAsync()
    {
        if (_registry is null) return;
        await _registry.SetActiveAsync(installId: null, LlamaServerSource.Manual);
        await UnloadRecognizerAsync();
        await ReloadInstalledAndActiveAsync();
    }

    // --- Helpers ---

    private async Task ReloadInstalledAndActiveAsync()
    {
        if (_registry is null) return;

        var installs = await _registry.ListManagedAsync();
        var active = await _registry.GetActiveAsync();
        var activeId = active?.Source == LlamaServerSource.Managed ? active.Id : null;
        var rows = installs
            .Select(i => new LlamaServerInstallRowVm(i, isActive: i.Id == activeId))
            .ToList();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Installed.Clear();
            foreach (var row in rows)
                Installed.Add(row);

            IsManagedActive = active is { Source: LlamaServerSource.Managed };
            IsManualActive = active is { Source: LlamaServerSource.Manual };
            ActiveManagedInstall = IsManagedActive
                ? rows.FirstOrDefault(r => r.IsActive)
                : null;
        });
    }

    private async Task PopulateAvailableAsync(IReadOnlyList<LlamaServerReleaseGroup> groups)
    {
        var installedIds = Installed.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var rows = new List<LlamaServerVariantRowVm>();
        foreach (var group in groups)
        {
            foreach (var variant in group.Variants)
            {
                var id = Platform.Speech.LlamaServer.LlamaServerInstaller.BuildInstallId(variant);
                rows.Add(new LlamaServerVariantRowVm(variant, alreadyInstalled: installedIds.Contains(id)));
            }
        }

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Available.Clear();
            foreach (var row in rows)
                Available.Add(row);
        });
    }

    private async Task RecomputeAvailableInstalledFlagsAsync()
    {
        var installedIds = Installed.Select(i => i.Id).ToHashSet(StringComparer.Ordinal);
        var rebuilt = Available
            .Select(v => new LlamaServerVariantRowVm(
                v.Variant, alreadyInstalled: installedIds.Contains(
                    Platform.Speech.LlamaServer.LlamaServerInstaller.BuildInstallId(v.Variant))))
            .ToList();

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            Available.Clear();
            foreach (var row in rebuilt)
                Available.Add(row);
        });
    }

    private void UpdateUpdateBanner(IReadOnlyList<LlamaServerReleaseGroup> groups)
    {
        var latest = groups.FirstOrDefault()?.Build;
        LatestAvailableBuild = latest;

        if (latest is null)
        {
            IsUpdateAvailable = false;
            return;
        }

        var activeBuild = ActiveManagedInstall?.Build;
        IsUpdateAvailable = activeBuild is not null
            && !string.Equals(activeBuild, latest, StringComparison.Ordinal);
    }

    private async Task UnloadRecognizerAsync()
    {
        if (_recognizer is not { IsReady: true })
            return;

        _logger.LogInformation("Unloading speech recognizer after settings change");
        await _recognizer.UnloadAsync();
    }

    private void ClearServerProps()
    {
        ModelAlias = null;
        ModelPath = null;
        AudioSupported = false;
        BuildInfo = null;
    }

    private sealed class DesignSettingsService : ISettingsService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
