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

    /// <summary>
    /// Fixed-length dot mask shown for a stored key. Deliberately a constant —
    /// it does not reflect the real key or its length, so nothing is read back
    /// from the secret store to render it.
    /// </summary>
    public const string KeyMask = "●●●●●●●●●●●●●●●●";

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

    /// <summary>Inline validation hint (null = valid). HTTPS-or-loopback rule, security audit 2026-07-11 S3.</summary>
    [ObservableProperty]
    private string? _openAiBaseUrlError;

    [ObservableProperty]
    private string _openAiModel = "";

    /// <summary>Write-only key entry field — cleared immediately after a successful save, never populated from storage.</summary>
    [ObservableProperty]
    private string _openAiKeyEntry = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(OpenAiKeyStatus))]
    [NotifyPropertyChangedFor(nameof(ShowOpenAiKeyEntry))]
    [NotifyPropertyChangedFor(nameof(ShowOpenAiKeySaved))]
    [NotifyPropertyChangedFor(nameof(CanCancelOpenAiEdit))]
    private bool _hasOpenAiKey;

    /// <summary>True while the user is entering a replacement for an already-stored key (Change clicked).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowOpenAiKeyEntry))]
    [NotifyPropertyChangedFor(nameof(ShowOpenAiKeySaved))]
    [NotifyPropertyChangedFor(nameof(CanCancelOpenAiEdit))]
    private bool _isEditingOpenAiKey;

    /// <summary>Eye toggle: when true the entry box reveals the key being typed.</summary>
    [ObservableProperty]
    private bool _isOpenAiKeyRevealed;

    public string OpenAiKeyStatus => HasOpenAiKey ? "Key saved" : "No key";

    /// <summary>Show the editable entry row: no key yet, or replacing an existing one.</summary>
    public bool ShowOpenAiKeyEntry => !HasOpenAiKey || IsEditingOpenAiKey;

    /// <summary>Show the read-only masked "saved" row.</summary>
    public bool ShowOpenAiKeySaved => HasOpenAiKey && !IsEditingOpenAiKey;

    /// <summary>Offer Cancel only when a stored key exists to fall back to.</summary>
    public bool CanCancelOpenAiEdit => HasOpenAiKey && IsEditingOpenAiKey;

    // ----- xAI Grok ------------------------------------------------------------

    [ObservableProperty]
    private string _xaiBaseUrl = "";

    /// <summary>Inline validation hint (null = valid). HTTPS-or-loopback rule, security audit 2026-07-11 S3.</summary>
    [ObservableProperty]
    private string? _xaiBaseUrlError;

    [ObservableProperty]
    private string _xaiModel = "";

    /// <summary>Write-only key entry field — cleared immediately after a successful save, never populated from storage.</summary>
    [ObservableProperty]
    private string _xaiKeyEntry = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(XaiKeyStatus))]
    [NotifyPropertyChangedFor(nameof(ShowXaiKeyEntry))]
    [NotifyPropertyChangedFor(nameof(ShowXaiKeySaved))]
    [NotifyPropertyChangedFor(nameof(CanCancelXaiEdit))]
    private bool _hasXaiKey;

    /// <summary>True while the user is entering a replacement for an already-stored key (Change clicked).</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowXaiKeyEntry))]
    [NotifyPropertyChangedFor(nameof(ShowXaiKeySaved))]
    [NotifyPropertyChangedFor(nameof(CanCancelXaiEdit))]
    private bool _isEditingXaiKey;

    /// <summary>Eye toggle: when true the entry box reveals the key being typed.</summary>
    [ObservableProperty]
    private bool _isXaiKeyRevealed;

    public string XaiKeyStatus => HasXaiKey ? "Key saved" : "No key";

    /// <summary>Show the editable entry row: no key yet, or replacing an existing one.</summary>
    public bool ShowXaiKeyEntry => !HasXaiKey || IsEditingXaiKey;

    /// <summary>Show the read-only masked "saved" row.</summary>
    public bool ShowXaiKeySaved => HasXaiKey && !IsEditingXaiKey;

    /// <summary>Offer Cancel only when a stored key exists to fall back to.</summary>
    public bool CanCancelXaiEdit => HasXaiKey && IsEditingXaiKey;

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
        // The value is persisted regardless (no silent data loss while typing);
        // the recognizer re-validates at initialisation and refuses to send the
        // key over a rejected URL, so the hint here is a courtesy, not the gate.
        OpenAiBaseUrlError = CloudBaseUrlValidator.TryValidate(value, out var error) ? null : error;

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
        // See OnOpenAiBaseUrlChanged — hint only; the recognizer is the gate.
        XaiBaseUrlError = CloudBaseUrlValidator.TryValidate(value, out var error) ? null : error;

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
        IsEditingOpenAiKey = false;
        IsOpenAiKeyRevealed = false;
        HasOpenAiKey = true;
        _logger.LogInformation("OpenAI-compatible API key saved");
    }

    /// <summary>Reveals the entry row over an existing key so the user can enter a replacement.</summary>
    [RelayCommand]
    private void ChangeOpenAiKey()
    {
        OpenAiKeyEntry = "";
        IsOpenAiKeyRevealed = false;
        IsEditingOpenAiKey = true;
    }

    /// <summary>Abandons a replacement, keeping the stored key and returning to the saved view.</summary>
    [RelayCommand]
    private void CancelEditOpenAiKey()
    {
        OpenAiKeyEntry = "";
        IsOpenAiKeyRevealed = false;
        IsEditingOpenAiKey = false;
    }

    [RelayCommand]
    private async Task RemoveOpenAiKeyAsync()
    {
        await _secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, null);
        OpenAiKeyEntry = "";
        IsEditingOpenAiKey = false;
        IsOpenAiKeyRevealed = false;
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
        IsEditingXaiKey = false;
        IsXaiKeyRevealed = false;
        HasXaiKey = true;
        _logger.LogInformation("xAI Grok API key saved");
    }

    /// <summary>Reveals the entry row over an existing key so the user can enter a replacement.</summary>
    [RelayCommand]
    private void ChangeXaiKey()
    {
        XaiKeyEntry = "";
        IsXaiKeyRevealed = false;
        IsEditingXaiKey = true;
    }

    /// <summary>Abandons a replacement, keeping the stored key and returning to the saved view.</summary>
    [RelayCommand]
    private void CancelEditXaiKey()
    {
        XaiKeyEntry = "";
        IsXaiKeyRevealed = false;
        IsEditingXaiKey = false;
    }

    [RelayCommand]
    private async Task RemoveXaiKeyAsync()
    {
        await _secrets.SetAsync(SettingsKeys.XaiGrokApiKey, null);
        XaiKeyEntry = "";
        IsEditingXaiKey = false;
        IsXaiKeyRevealed = false;
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
