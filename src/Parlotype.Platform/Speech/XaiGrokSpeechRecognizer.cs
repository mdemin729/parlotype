using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Speech recognizer for xAI's Grok Speech-to-Text REST API
/// (<c>POST {baseUrl}/stt</c>). Not OpenAI-compatible — different endpoint
/// path and form field names (<c>format</c> instead of
/// <c>response_format</c>) — so this is a dedicated client rather than a
/// base-URL swap on <see cref="OpenAiCompatibleSpeechRecognizer"/>. Cloud,
/// opt-in, bring-your-own-key: audio and the API key leave this machine and
/// go directly to xAI, no Parlotype server involved (ADR-032).
/// </summary>
/// <remarks>
/// Text post-processing (punctuation stripping, profanity filtering) is
/// deliberately <em>not</em> applied here — <c>AudioPipelineService</c>
/// already runs every recognizer's result through
/// <see cref="TranscriptionTextProcessor"/> uniformly after
/// <see cref="TranscribeAsync"/> returns, so doing it again here would
/// double-process the text.
/// </remarks>
public sealed class XaiGrokSpeechRecognizer : ISpeechRecognizer
{
    private const string DefaultBaseUrl = "https://api.x.ai/v1";
    private const string DefaultModel = "grok-stt";
    private const string ProviderDisplayName = "xAI Grok";
    private const int SampleRate = 16_000;

    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly IKeyboardLayoutService _keyboardLayout;
    private readonly ILogger<XaiGrokSpeechRecognizer> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // Recreated on each Initialize — see OpenAiCompatibleSpeechRecognizer's
    // identical field comment (HttpClient locks its default headers after
    // first use).
    private HttpClient? _httpClient;
    private string _baseUrl = DefaultBaseUrl;
    private string _model = DefaultModel;
    private bool _disposed;

    /// <summary>
    /// Testability seam: when set, <see cref="InitializeAsync(CancellationToken)"/>
    /// builds the HttpClient around this handler instead of the real socket
    /// handler, so tests can script HTTP responses with no network access.
    /// </summary>
    internal HttpMessageHandler? MessageHandlerOverride { get; set; }

    public bool IsReady { get; private set; }

    public XaiGrokSpeechRecognizer(
        ISettingsService settings,
        ISecretStore secrets,
        IKeyboardLayoutService keyboardLayout,
        ILogger<XaiGrokSpeechRecognizer> logger)
    {
        _settings = settings;
        _secrets = secrets;
        _keyboardLayout = keyboardLayout;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsReady)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (IsReady)
                return;

            var savedBaseUrl = await _settings.GetAsync<string>(SettingsKeys.XaiGrokBaseUrl, cancellationToken);
            _baseUrl = string.IsNullOrWhiteSpace(savedBaseUrl) ? DefaultBaseUrl : savedBaseUrl.TrimEnd('/');

            var savedModel = await _settings.GetAsync<string>(SettingsKeys.XaiGrokModel, cancellationToken);
            _model = string.IsNullOrWhiteSpace(savedModel) ? DefaultModel : savedModel;

            var apiKey = await _secrets.GetAsync(SettingsKeys.XaiGrokApiKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "No API key configured for the xAI Grok provider. Add one in Settings → Speech engine.");

            _httpClient?.Dispose();
            _httpClient = MessageHandlerOverride is not null
                ? new HttpClient(MessageHandlerOverride, disposeHandler: false)
                : new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            // Never log apiKey or this header — see class remarks.
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            IsReady = true;
            _logger.LogInformation(
                "xAI Grok recognizer initialized (baseUrl: {BaseUrl}, model: {Model})",
                _baseUrl, _model);
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsReady || _httpClient is null)
            throw new InvalidOperationException("Recognizer is not initialized. Call InitializeAsync first.");

        var wavBytes = WavEncoder.Encode(samples.Span, SampleRate);
        // xAI Grok publishes its own language set rather than reusing Whisper's
        // curated list (SpeechEngineCapabilities.For(SpeechEngine.XaiGrok) passes
        // null = full catalog), so no supported-list filter is applied here either.
        var languageCode = await CloudSpeechLanguageResolver.ResolveAsync(
            _settings, _keyboardLayout, supported: null, cancellationToken);

        using var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        using var content = new MultipartFormDataContent
        {
            { fileContent, "file", "audio.wav" },
            { new StringContent(_model), "model" },
            { new StringContent("json"), "format" },
        };

        if (languageCode is not null)
            content.Add(new StringContent(languageCode), "language");

        _logger.LogDebug(
            "Transcribing {SampleCount} samples via {Provider} ({BaseUrl}, model: {Model})",
            samples.Length, ProviderDisplayName, _baseUrl, _model);

        using var response = await _httpClient.PostAsync($"{_baseUrl}/stt", content, cancellationToken);

        if (!response.IsSuccessStatusCode)
            throw await CloudSpeechHttpError.BuildAsync(response, ProviderDisplayName, _logger, cancellationToken);

        var text = await ParseTextAsync(response, cancellationToken);

        return new TranscriptionResult
        {
            Text = text,
            DetectedLanguage = null,
            Confidence = null,
        };
    }

    /// <summary>Parses the response text, preferring <c>text</c> and falling back to <c>transcript</c>.</summary>
    private static async Task<string> ParseTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var text = doc.RootElement.TryGetProperty("text", out var textElement)
            ? textElement.GetString()
            : null;

        if (string.IsNullOrEmpty(text) && doc.RootElement.TryGetProperty("transcript", out var transcriptElement))
            text = transcriptElement.GetString();

        return (text ?? string.Empty).Trim();
    }

    public async Task UnloadAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _initLock.WaitAsync();
        try
        {
            _httpClient?.Dispose();
            _httpClient = null;
            IsReady = false;
            _logger.LogInformation("xAI Grok recognizer unloaded");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        // Unload before setting _disposed so UnloadAsync doesn't throw
        await UnloadAsync().ConfigureAwait(false);
        _disposed = true;
        _initLock.Dispose();
    }
}
