using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
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
    private readonly ILogger<LlamaCppSettingsViewModel> _logger;

    public override string Title => "llama.cpp";

    // --- Server status ---

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

    // --- User-editable settings ---

    [ObservableProperty]
    private string _portText = DefaultPort.ToString();

    [ObservableProperty]
    private string _serverFolder = "";

    [ObservableProperty]
    private bool _isRefreshing;

    public LlamaCppSettingsViewModel(
        ISettingsService settings,
        ISpeechRecognizer? recognizer = null,
        ILogger<LlamaCppSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _recognizer = recognizer;
        _logger = logger ?? NullLogger<LlamaCppSettingsViewModel>.Instance;

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
        // Validate port
        if (!int.TryParse(PortText, out var port) || port is <= 0 or > 65535)
        {
            ErrorMessage = "Port must be a number between 1 and 65535.";
            return;
        }

        // Validate folder
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

        // Unload current recognizer so it re-initializes with new settings
        await UnloadRecognizerAsync();

        // Re-probe with the new port
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
