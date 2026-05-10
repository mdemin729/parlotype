using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Platform.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

public partial class LlamaCppSettingsViewModel : SettingsSectionViewModelBase
{
    private const int DefaultPort = 8321;
    private const string DefaultHost = "127.0.0.1";

    private readonly ISettingsService _settings;
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
    private string _serverPath = "";

    [ObservableProperty]
    private bool _isRefreshing;

    public LlamaCppSettingsViewModel(
        ISettingsService settings,
        ILogger<LlamaCppSettingsViewModel>? logger = null)
    {
        _settings = settings;
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

        var path = await _settings.GetAsync<string>(SettingsKeys.LlamaCppServerPath);
        ServerPath = path ?? @"C:\ai\llama-b9090-bin-win-vulkan-x64\llama-server.exe";
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
    private async Task SavePortAsync()
    {
        if (!int.TryParse(PortText, out var port) || port is <= 0 or > 65535)
        {
            ErrorMessage = "Port must be a number between 1 and 65535.";
            return;
        }

        await _settings.SetAsync(SettingsKeys.LlamaCppPort, port.ToString());
        _logger.LogInformation("llama.cpp port saved: {Port}", port);
        ErrorMessage = null;

        // Re-probe with the new port
        await RefreshServerInfoAsync();
    }

    [RelayCommand]
    private async Task SaveServerPathAsync()
    {
        var path = ServerPath?.Trim();
        if (string.IsNullOrEmpty(path))
        {
            ErrorMessage = "Server path cannot be empty.";
            return;
        }

        await _settings.SetAsync(SettingsKeys.LlamaCppServerPath, path);
        _logger.LogInformation("llama.cpp server path saved: {Path}", path);
        ErrorMessage = null;
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
