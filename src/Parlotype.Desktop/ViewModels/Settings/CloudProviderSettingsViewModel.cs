using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Desktop.ViewModels.Settings;

/// <summary>
/// Settings → Speech engine → Cloud providers: base URL / model / API key
/// management for the two opt-in, bring-your-own-key cloud transcription
/// engines (<see cref="SpeechEngine.OpenAiCompatible"/>, <see cref="SpeechEngine.XaiGrok"/>;
/// ADR-032). Lives under <see cref="SettingsCategory.SpeechEngine"/> so the
/// recognizers' "configure your key in Settings → Speech engine" error text
/// stays accurate, and is visible whenever either cloud engine is active
/// (<see cref="IsVisibleFor"/>) — the same engine-scoped visibility mechanism
/// the Language page uses, generalized from a single engine to a set.
/// </summary>
public partial class CloudProviderSettingsViewModel : SettingsSectionViewModelBase
{
    /// <summary>Default base URL shown as the OpenAI-compatible field's watermark when unset.</summary>
    public const string DefaultOpenAiBaseUrl = "https://api.openai.com/v1";

    /// <summary>Default model id shown as the OpenAI-compatible field's watermark when unset.</summary>
    public const string DefaultOpenAiModel = "gpt-4o-mini-transcribe";

    /// <summary>Default base URL shown as the xAI Grok field's watermark when unset.</summary>
    public const string DefaultXaiBaseUrl = "https://api.x.ai/v1";

    /// <summary>Default model id shown as the xAI Grok field's watermark when unset.</summary>
    public const string DefaultXaiModel = "grok-stt";

    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly ILogger<CloudProviderSettingsViewModel> _logger;

    /// <summary>Guards persistence writes while <see cref="InitializeAsync"/> is populating fields from storage.</summary>
    private bool _isLoading = true;

    public override string Title => "Cloud providers";
    public override SettingsCategory Category => SettingsCategory.SpeechEngine;

    public override bool IsVisibleFor(SpeechEngine engine) =>
        engine is SpeechEngine.OpenAiCompatible or SpeechEngine.XaiGrok;

    // ----- OpenAI-compatible -------------------------------------------------

    [ObservableProperty]
    private string _openAiBaseUrl = "";

    [ObservableProperty]
    private string _openAiModel = "";

    /// <summary>Write-only key entry field — cleared immediately after a successful save, never populated from storage.</summary>
    [ObservableProperty]
    private string _openAiKeyEntry = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenAiKeyStatus))]
    private bool _hasOpenAiKey;

    public string OpenAiKeyStatus => HasOpenAiKey ? "Key saved" : "No key";

    // ----- xAI Grok ------------------------------------------------------------

    [ObservableProperty]
    private string _xaiBaseUrl = "";

    [ObservableProperty]
    private string _xaiModel = "";

    /// <summary>Write-only key entry field — cleared immediately after a successful save, never populated from storage.</summary>
    [ObservableProperty]
    private string _xaiKeyEntry = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(XaiKeyStatus))]
    private bool _hasXaiKey;

    public string XaiKeyStatus => HasXaiKey ? "Key saved" : "No key";

    public CloudProviderSettingsViewModel(
        ISettingsService settings,
        ISecretStore secrets,
        ILogger<CloudProviderSettingsViewModel>? logger = null)
    {
        _settings = settings;
        _secrets = secrets;
        _logger = logger ?? NullLogger<CloudProviderSettingsViewModel>.Instance;

        _ = InitializeAsync();
    }

    /// <summary>Parameterless constructor for designer support only.</summary>
    public CloudProviderSettingsViewModel() : this(new DesignSettingsService(), new DesignSecretStore()) { }

    private async Task InitializeAsync()
    {
        OpenAiBaseUrl = await _settings.GetAsync<string>(SettingsKeys.OpenAiCompatBaseUrl) ?? "";
        OpenAiModel = await _settings.GetAsync<string>(SettingsKeys.OpenAiCompatModel) ?? "";
        XaiBaseUrl = await _settings.GetAsync<string>(SettingsKeys.XaiGrokBaseUrl) ?? "";
        XaiModel = await _settings.GetAsync<string>(SettingsKeys.XaiGrokModel) ?? "";

        HasOpenAiKey = await _secrets.GetAsync(SettingsKeys.OpenAiCompatApiKey) is not null;
        HasXaiKey = await _secrets.GetAsync(SettingsKeys.XaiGrokApiKey) is not null;

        _isLoading = false;
    }

    partial void OnOpenAiBaseUrlChanged(string value)
    {
        if (_isLoading) return;
        _ = _settings.SetAsync(SettingsKeys.OpenAiCompatBaseUrl, value);
    }

    partial void OnOpenAiModelChanged(string value)
    {
        if (_isLoading) return;
        _ = _settings.SetAsync(SettingsKeys.OpenAiCompatModel, value);
    }

    partial void OnXaiBaseUrlChanged(string value)
    {
        if (_isLoading) return;
        _ = _settings.SetAsync(SettingsKeys.XaiGrokBaseUrl, value);
    }

    partial void OnXaiModelChanged(string value)
    {
        if (_isLoading) return;
        _ = _settings.SetAsync(SettingsKeys.XaiGrokModel, value);
    }

    [RelayCommand]
    private async Task SaveOpenAiKeyAsync()
    {
        var key = OpenAiKeyEntry.Trim();
        if (string.IsNullOrEmpty(key))
            return;

        await _secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, key);
        OpenAiKeyEntry = "";
        HasOpenAiKey = true;
        _logger.LogInformation("OpenAI-compatible API key saved");
    }

    [RelayCommand]
    private async Task RemoveOpenAiKeyAsync()
    {
        await _secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, null);
        HasOpenAiKey = false;
        _logger.LogInformation("OpenAI-compatible API key removed");
    }

    [RelayCommand]
    private async Task SaveXaiKeyAsync()
    {
        var key = XaiKeyEntry.Trim();
        if (string.IsNullOrEmpty(key))
            return;

        await _secrets.SetAsync(SettingsKeys.XaiGrokApiKey, key);
        XaiKeyEntry = "";
        HasXaiKey = true;
        _logger.LogInformation("xAI Grok API key saved");
    }

    [RelayCommand]
    private async Task RemoveXaiKeyAsync()
    {
        await _secrets.SetAsync(SettingsKeys.XaiGrokApiKey, null);
        HasXaiKey = false;
        _logger.LogInformation("xAI Grok API key removed");
    }

    private sealed class DesignSettingsService : ISettingsService
    {
        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<T?>(default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class DesignSecretStore : ISecretStore
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) =>
            Task.FromResult<string?>(null);

        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
