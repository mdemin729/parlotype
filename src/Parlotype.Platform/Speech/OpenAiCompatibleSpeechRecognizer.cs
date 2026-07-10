using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Speech recognizer for the OpenAI transcription REST protocol
/// (<c>POST {baseUrl}/audio/transcriptions</c>) — works against OpenAI
/// itself, Groq, or any other host that speaks the same multipart contract
/// via a configurable base URL. Cloud, opt-in, bring-your-own-key: audio and
/// the API key leave this machine and go directly to the configured host, no
/// Parlotype server involved (ADR-032).
/// </summary>
/// <remarks>
/// Text post-processing (punctuation stripping, profanity filtering) is
/// deliberately <em>not</em> applied here — <c>AudioPipelineService</c>
/// already runs every recognizer's result through
/// <see cref="TranscriptionTextProcessor"/> uniformly after
/// <see cref="TranscribeAsync"/> returns, so doing it again here would
/// double-process the text.
/// </remarks>
public sealed class OpenAiCompatibleSpeechRecognizer : ISpeechRecognizer
{
    private const string DefaultBaseUrl = "https://api.openai.com/v1";
    private const string DefaultModel = "gpt-4o-mini-transcribe";
    private const string ProviderDisplayName = "OpenAI-compatible provider";
    private const int SampleRate = 16_000;

    private readonly ISettingsService _settings;
    private readonly ISecretStore _secrets;
    private readonly IKeyboardLayoutService _keyboardLayout;
    private readonly ILogger<OpenAiCompatibleSpeechRecognizer> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);

    // Recreated on each Initialize — HttpClient locks BaseAddress/default headers
    // after first use, so reusing the same instance across unload/init cycles
    // (e.g. the user rotates their key) would be unsafe (see
    // LlamaCppSpeechRecognizer's identical field comment).
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

    public OpenAiCompatibleSpeechRecognizer(
        ISettingsService settings,
        ISecretStore secrets,
        IKeyboardLayoutService keyboardLayout,
        ILogger<OpenAiCompatibleSpeechRecognizer> logger)
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

            var savedBaseUrl = await _settings.GetAsync<string>(SettingsKeys.OpenAiCompatBaseUrl, cancellationToken);
            _baseUrl = string.IsNullOrWhiteSpace(savedBaseUrl) ? DefaultBaseUrl : savedBaseUrl.TrimEnd('/');

            var savedModel = await _settings.GetAsync<string>(SettingsKeys.OpenAiCompatModel, cancellationToken);
            _model = string.IsNullOrWhiteSpace(savedModel) ? DefaultModel : savedModel;

            var apiKey = await _secrets.GetAsync(SettingsKeys.OpenAiCompatApiKey, cancellationToken);
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException(
                    "No API key configured for the OpenAI-compatible provider. " +
                    "Add one in Settings → Speech engine.");

            _httpClient?.Dispose();
            _httpClient = MessageHandlerOverride is not null
                ? new HttpClient(MessageHandlerOverride, disposeHandler: false)
                : new HttpClient();
            _httpClient.Timeout = TimeSpan.FromSeconds(60);
            // Never log apiKey or this header — see class remarks.
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            IsReady = true;
            _logger.LogInformation(
                "OpenAI-compatible recognizer initialized (baseUrl: {BaseUrl}, model: {Model})",
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
        var languageCode = await CloudSpeechLanguageResolver.ResolveAsync(
            _settings, _keyboardLayout, LanguageCatalog.WhisperLanguages, cancellationToken);

        using var fileContent = new ByteArrayContent(wavBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("audio/wav");

        using var content = new MultipartFormDataContent
        {
            { fileContent, "file", "audio.wav" },
            { new StringContent(_model), "model" },
            { new StringContent("json"), "response_format" },
            { new StringContent("0"), "temperature" },
        };

        if (languageCode is not null)
            content.Add(new StringContent(languageCode), "language");

        _logger.LogDebug(
            "Transcribing {SampleCount} samples via {Provider} ({BaseUrl}, model: {Model})",
            samples.Length, ProviderDisplayName, _baseUrl, _model);

        using var response = await _httpClient.PostAsync(
            $"{_baseUrl}/audio/transcriptions", content, cancellationToken);

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

    private static async Task<string> ParseTextAsync(HttpResponseMessage response, CancellationToken cancellationToken)
    {
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var text = doc.RootElement.TryGetProperty("text", out var textElement)
            ? textElement.GetString() ?? string.Empty
            : string.Empty;

        return text.Trim();
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
            _logger.LogInformation("OpenAI-compatible recognizer unloaded");
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
