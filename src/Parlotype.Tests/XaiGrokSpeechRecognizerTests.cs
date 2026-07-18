using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public sealed class XaiGrokSpeechRecognizerTests
{
    private sealed class FakeSettingsService : ISettingsService
    {
        private readonly Dictionary<string, object?> _store = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.TryGetValue(key, out var v) ? (T?)v : default);

        public Task SetAsync<T>(string key, T value, CancellationToken cancellationToken = default)
        {
            _store[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeSecretStore : ISecretStore
    {
        private readonly Dictionary<string, string?> _store = new();

        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
            => Task.FromResult(_store.GetValueOrDefault(key));

        public Task SetAsync(string key, string? value, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrEmpty(value))
                _store.Remove(key);
            else
                _store[key] = value;
            return Task.CompletedTask;
        }
    }

    private sealed record CapturedRequest(Uri Uri, AuthenticationHeaderValue? Authorization, string Body);

    private sealed class ScriptedHandler : HttpMessageHandler
    {
        private readonly Queue<Func<HttpRequestMessage, HttpResponseMessage>> _responders = new();

        public List<CapturedRequest> Requests { get; } = new();

        public void Enqueue(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responders.Enqueue(responder);

        public void EnqueueJson(HttpStatusCode status, string json)
        {
            _responders.Enqueue(_ => new HttpResponseMessage(status)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            });
        }

        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null
                ? string.Empty
                : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(request.RequestUri!, request.Headers.Authorization, body));

            if (_responders.Count == 0)
                throw new InvalidOperationException("No scripted response left.");

            return _responders.Dequeue()(request);
        }
    }

    /// <summary>
    /// Extracts the value of a multipart/form-data field by name, tolerant of
    /// whichever header order/quoting <see cref="MultipartFormDataContent"/>
    /// happens to serialize. Returns null when the field is absent.
    /// </summary>
    private static string? ExtractFormPartValue(string body, string fieldName)
    {
        var nameIndex = body.IndexOf($"name=\"{fieldName}\"", StringComparison.Ordinal);
        if (nameIndex < 0)
            nameIndex = body.IndexOf($"name={fieldName}", StringComparison.Ordinal);
        if (nameIndex < 0)
            return null;

        var headerEnd = body.IndexOf("\r\n\r\n", nameIndex, StringComparison.Ordinal);
        if (headerEnd < 0)
            return null;

        var valueStart = headerEnd + 4;
        var valueEnd = body.IndexOf("\r\n--", valueStart, StringComparison.Ordinal);
        return valueEnd < 0 ? body[valueStart..] : body[valueStart..valueEnd];
    }

    private static XaiGrokSpeechRecognizer Create(
        ScriptedHandler handler,
        FakeSettingsService? settings = null,
        FakeSecretStore? secrets = null)
    {
        var recognizer = new XaiGrokSpeechRecognizer(
            settings ?? new FakeSettingsService(),
            secrets ?? new FakeSecretStore(),
            NullLogger<XaiGrokSpeechRecognizer>.Instance)
        {
            MessageHandlerOverride = handler,
        };
        return recognizer;
    }

    private static async Task<(XaiGrokSpeechRecognizer Recognizer, FakeSettingsService Settings, FakeSecretStore Secrets)>
        CreateInitializedAsync(ScriptedHandler handler)
    {
        var settings = new FakeSettingsService();
        var secrets = new FakeSecretStore();
        await secrets.SetAsync(SettingsKeys.XaiGrokApiKey, "xai-test-key");

        var recognizer = Create(handler, settings, secrets);
        await recognizer.InitializeAsync();
        return (recognizer, settings, secrets);
    }

    [Fact]
    public async Task InitializeAsync_MissingApiKey_ThrowsActionableMessage()
    {
        var recognizer = Create(new ScriptedHandler());

        // Typed exception so the UI can route the user to the cloud settings.
        var ex = await Assert.ThrowsAsync<CloudProviderNotConfiguredException>(() => recognizer.InitializeAsync());

        Assert.Equal(SpeechEngine.XaiGrok, ex.Engine);
        Assert.Contains("No API key configured for the xAI Grok provider", ex.Message);
        Assert.Contains("Settings", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_BeforeInitialize_Throws()
    {
        var recognizer = Create(new ScriptedHandler());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => recognizer.TranscribeAsync(new float[16_000]));
    }

    [Fact]
    public async Task TranscribeAsync_SendsRequestToExpectedUrl()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://api.x.ai/v1/stt", request.Uri.ToString());
    }

    [Fact]
    public async Task TranscribeAsync_HonorsConfiguredBaseUrlAndModel()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.XaiGrokBaseUrl, "https://xai.example.com/v1/");
        await settings.SetAsync(SettingsKeys.XaiGrokModel, "grok-stt-large");
        var secrets = new FakeSecretStore();
        await secrets.SetAsync(SettingsKeys.XaiGrokApiKey, "xai-test-key");

        var recognizer = Create(handler, settings, secrets);
        await recognizer.InitializeAsync();
        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("https://xai.example.com/v1/stt", request.Uri.ToString());
        Assert.Equal("grok-stt-large", ExtractFormPartValue(request.Body, "model"));
    }

    [Fact]
    public async Task TranscribeAsync_SendsBearerAuthorizationHeader()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        Assert.NotNull(request.Authorization);
        Assert.Equal("Bearer", request.Authorization!.Scheme);
        Assert.Equal("xai-test-key", request.Authorization.Parameter);
    }

    [Fact]
    public async Task TranscribeAsync_MultipartBody_ContainsFileModelAndFormat()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("name=file", request.Body);
        Assert.Contains("filename=audio.wav", request.Body);
        Assert.Contains("Content-Type: audio/wav", request.Body);
        Assert.Equal("grok-stt", ExtractFormPartValue(request.Body, "model"));
        // xAI's own parameter name — not response_format like OpenAI.
        Assert.Equal("json", ExtractFormPartValue(request.Body, "format"));
        Assert.Null(ExtractFormPartValue(request.Body, "response_format"));
    }

    [Fact]
    public async Task TranscribeAsync_NeverSendsLanguagePart_EvenWithPersistedSourceLanguage()
    {
        // Cloud engines always auto-detect (SupportsSourceSelection is false,
        // language UI hidden — ADR-043): a leftover SelectedSourceLanguage from
        // a local engine must not silently force a language on cloud requests.
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, "de");
        var secrets = new FakeSecretStore();
        await secrets.SetAsync(SettingsKeys.XaiGrokApiKey, "xai-test-key");

        var recognizer = Create(handler, settings, secrets);
        await recognizer.InitializeAsync();
        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        Assert.Null(ExtractFormPartValue(request.Body, "language"));
    }

    [Fact]
    public async Task TranscribeAsync_ParsesTextProperty_WhenPresent()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"  hello world  "}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        var result = await recognizer.TranscribeAsync(new float[16_000]);

        Assert.Equal("hello world", result.Text);
        Assert.Null(result.DetectedLanguage);
        Assert.Null(result.Confidence);
    }

    [Fact]
    public async Task TranscribeAsync_FallsBackToTranscriptProperty_WhenTextAbsent()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"transcript":"  fallback text  "}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        var result = await recognizer.TranscribeAsync(new float[16_000]);

        Assert.Equal("fallback text", result.Text);
    }

    [Fact]
    public async Task TranscribeAsync_Unauthorized_ThrowsKeyRejected()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, """{"error":"invalid_api_key"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        var ex = await Assert.ThrowsAsync<CloudSpeechTranscriptionException>(
            () => recognizer.TranscribeAsync(new float[16_000]));

        Assert.Equal(CloudSpeechErrorKind.KeyRejected, ex.Kind);
        Assert.Contains("rejected the API key", ex.Message);
        Assert.Contains("invalid_api_key", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_ServerError_ThrowsProviderUnavailable()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.InternalServerError, """{"error":"boom"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        var ex = await Assert.ThrowsAsync<CloudSpeechTranscriptionException>(
            () => recognizer.TranscribeAsync(new float[16_000]));

        Assert.Equal(CloudSpeechErrorKind.ProviderUnavailable, ex.Kind);
        Assert.Contains("500", ex.Message);
        Assert.Contains("boom", ex.Message);
    }

    [Fact]
    public async Task UnloadAsync_ThenReinitialize_Works()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"first"}""");
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"second"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        var first = await recognizer.TranscribeAsync(new float[16_000]);
        await recognizer.UnloadAsync();
        Assert.False(recognizer.IsReady);

        await recognizer.InitializeAsync();
        Assert.True(recognizer.IsReady);
        var second = await recognizer.TranscribeAsync(new float[16_000]);

        Assert.Equal("first", first.Text);
        Assert.Equal("second", second.Text);
    }

    /// <summary>HTTPS-or-loopback rule, security audit 2026-07-11 S3.</summary>
    [Fact]
    public async Task InitializeAsync_RemoteHttpBaseUrl_ThrowsNotConfigured()
    {
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.XaiGrokBaseUrl, "http://api.example.com/v1");
        var secrets = new FakeSecretStore();
        await secrets.SetAsync(SettingsKeys.XaiGrokApiKey, "xai-test");

        var recognizer = Create(new ScriptedHandler(), settings, secrets);

        var ex = await Assert.ThrowsAsync<CloudProviderNotConfiguredException>(
            () => recognizer.InitializeAsync());
        Assert.Contains("base URL", ex.Message);
        Assert.False(recognizer.IsReady);
    }
}
