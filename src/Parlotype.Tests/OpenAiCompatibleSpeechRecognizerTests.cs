using System.Net;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Parlotype.Platform.Speech;
using Xunit;

namespace Parlotype.Tests;

public sealed class OpenAiCompatibleSpeechRecognizerTests
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
    /// happens to serialize (e.g. an extra Content-Type line for
    /// <see cref="StringContent"/> parts). Returns null when the field is absent.
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

    private static OpenAiCompatibleSpeechRecognizer Create(
        ScriptedHandler handler,
        FakeSettingsService? settings = null,
        FakeSecretStore? secrets = null)
    {
        var recognizer = new OpenAiCompatibleSpeechRecognizer(
            settings ?? new FakeSettingsService(),
            secrets ?? new FakeSecretStore(),
            new NoOpKeyboardLayoutService(),
            NullLogger<OpenAiCompatibleSpeechRecognizer>.Instance)
        {
            MessageHandlerOverride = handler,
        };
        return recognizer;
    }

    private static async Task<(OpenAiCompatibleSpeechRecognizer Recognizer, FakeSettingsService Settings, FakeSecretStore Secrets)>
        CreateInitializedAsync(ScriptedHandler handler)
    {
        var settings = new FakeSettingsService();
        var secrets = new FakeSecretStore();
        await secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, "sk-test-key");

        var recognizer = Create(handler, settings, secrets);
        await recognizer.InitializeAsync();
        return (recognizer, settings, secrets);
    }

    [Fact]
    public async Task InitializeAsync_MissingApiKey_ThrowsActionableMessage()
    {
        var recognizer = Create(new ScriptedHandler());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => recognizer.InitializeAsync());

        Assert.Contains("No API key configured for the OpenAI-compatible provider", ex.Message);
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
        Assert.Equal("https://api.openai.com/v1/audio/transcriptions", request.Uri.ToString());
    }

    [Fact]
    public async Task TranscribeAsync_HonorsConfiguredBaseUrlAndModel()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.OpenAiCompatBaseUrl, "https://groq.example.com/openai/v1/");
        await settings.SetAsync(SettingsKeys.OpenAiCompatModel, "whisper-large-v3");
        var secrets = new FakeSecretStore();
        await secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, "sk-test-key");

        var recognizer = Create(handler, settings, secrets);
        await recognizer.InitializeAsync();
        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        // Trailing slash trimmed from the configured base URL.
        Assert.Equal("https://groq.example.com/openai/v1/audio/transcriptions", request.Uri.ToString());
        Assert.Contains("whisper-large-v3", request.Body);
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
        Assert.Equal("sk-test-key", request.Authorization.Parameter);
    }

    [Fact]
    public async Task TranscribeAsync_MultipartBody_ContainsFileModelAndResponseFormat()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        Assert.Contains("name=file", request.Body);
        Assert.Contains("filename=audio.wav", request.Body);
        Assert.Contains("Content-Type: audio/wav", request.Body);
        Assert.Equal("gpt-4o-mini-transcribe", ExtractFormPartValue(request.Body, "model"));
        Assert.Equal("json", ExtractFormPartValue(request.Body, "response_format"));
        Assert.Equal("0", ExtractFormPartValue(request.Body, "temperature"));
    }

    [Fact]
    public async Task TranscribeAsync_ConcreteSourceLanguage_AddsLanguagePart()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, "ru");
        var secrets = new FakeSecretStore();
        await secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, "sk-test-key");

        var recognizer = Create(handler, settings, secrets);
        await recognizer.InitializeAsync();
        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        Assert.Equal("ru", ExtractFormPartValue(request.Body, "language"));
    }

    [Fact]
    public async Task TranscribeAsync_AutoSourceLanguage_OmitsLanguagePart()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.OK, """{"text":"hello"}""");
        var settings = new FakeSettingsService();
        await settings.SetAsync(SettingsKeys.SelectedSourceLanguage, "auto");
        var secrets = new FakeSecretStore();
        await secrets.SetAsync(SettingsKeys.OpenAiCompatApiKey, "sk-test-key");

        var recognizer = Create(handler, settings, secrets);
        await recognizer.InitializeAsync();
        await recognizer.TranscribeAsync(new float[16_000]);

        var request = Assert.Single(handler.Requests);
        Assert.Null(ExtractFormPartValue(request.Body, "language"));
    }

    [Fact]
    public async Task TranscribeAsync_ParsesAndTrimsResponseText()
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
    public async Task TranscribeAsync_Unauthorized_ThrowsMentioningRejectedKey()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.Unauthorized, """{"error":"invalid_api_key"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => recognizer.TranscribeAsync(new float[16_000]));

        Assert.Contains("API key rejected", ex.Message);
    }

    [Fact]
    public async Task TranscribeAsync_ServerError_ThrowsWithStatusAndBody()
    {
        var handler = new ScriptedHandler();
        handler.EnqueueJson(HttpStatusCode.InternalServerError, """{"error":"boom"}""");
        var (recognizer, _, _) = await CreateInitializedAsync(handler);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => recognizer.TranscribeAsync(new float[16_000]));

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
}
