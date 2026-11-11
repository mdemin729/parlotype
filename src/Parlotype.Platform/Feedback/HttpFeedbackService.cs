using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Parlotype.Core.Feedback;

namespace Parlotype.Platform.Feedback;

/// <summary>
/// <see cref="IFeedbackService"/> over <c>POST https://parlotype.dev/api/feedback</c>
/// (ADR-070). The contract is <c>docs/feedback-api.md</c> in the website repository.
/// </summary>
/// <remarks>
/// <para>
/// One request per call: no retries, no queue, no background sends. The request carries
/// the JSON body and its content type — no authentication (there is no secret worth
/// shipping: anything in the binary can be extracted, and the server is protected by
/// limits instead), no cookies, and no <c>User-Agent</c>, which would otherwise leak the
/// version the user may have unticked.
/// </para>
/// <para>
/// Owns its <see cref="HttpClient"/> rather than sharing the app-wide one: that one has a
/// one-hour timeout for model downloads, and a private client is also what lets a test
/// hand in a fake <see cref="HttpMessageHandler"/> so nothing ever reaches parlotype.dev.
/// </para>
/// </remarks>
public sealed class HttpFeedbackService : IFeedbackService, IDisposable
{
    /// <summary>The contract asks for "about 15 seconds".</summary>
    public static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(15);

    /// <summary>
    /// Debug builds only: points the service at another endpoint, such as a local
    /// <c>wrangler dev</c> (<c>http://localhost:8787/api/feedback</c>). Release builds
    /// ignore it.
    /// </summary>
    public const string EndpointOverrideVariable = "PARLOTYPE_FEEDBACK_ENDPOINT";

    private static readonly MediaTypeHeaderValue JsonContentType = new("application/json") { CharSet = "utf-8" };

    private readonly HttpClient _http;
    private readonly Uri _endpoint;
    private readonly TimeSpan _timeout;
    private readonly ILogger _logger;
    private readonly Lazy<FeedbackDiagnostics> _diagnostics;

    public HttpFeedbackService(ILogger<HttpFeedbackService>? logger = null)
        : this(
            ResolveEndpoint(Environment.GetEnvironmentVariable),
            new SocketsHttpHandler
            {
                // A redirect would send the message somewhere the dialog did not name,
                // and would turn the POST into a GET anyway. Treat it as a failure.
                AllowAutoRedirect = false,
                UseCookies = false,
            },
            DefaultTimeout,
            diagnostics: null,
            logger)
    {
    }

    /// <summary>For tests: a fake handler, a short timeout and fixed diagnostics.</summary>
    internal HttpFeedbackService(
        Uri endpoint,
        HttpMessageHandler handler,
        TimeSpan timeout,
        FeedbackDiagnostics? diagnostics = null,
        ILogger? logger = null)
    {
        _endpoint = endpoint;
        _timeout = timeout;
        _logger = logger ?? NullLogger<HttpFeedbackService>.Instance;
        // The timeout is ours (below), so it can be told apart from the caller cancelling.
        _http = new HttpClient(handler, disposeHandler: true) { Timeout = Timeout.InfiniteTimeSpan };
        _diagnostics = diagnostics is not null
            ? new Lazy<FeedbackDiagnostics>(diagnostics)
            : new Lazy<FeedbackDiagnostics>(ReadDiagnostics);

        if (endpoint != FeedbackLinks.Endpoint)
            _logger.LogWarning("Feedback endpoint overridden: {Endpoint}", endpoint);
    }

    public FeedbackDiagnostics Diagnostics => _diagnostics.Value;

    /// <summary>The address requests go to — the production endpoint in every Release build.</summary>
    public Uri Endpoint => _endpoint;

    public async Task<FeedbackSendResult> SendAsync(
        FeedbackSubmission submission,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(submission);

        var message = FeedbackLimits.NormalizeMessage(submission.Message);
        if (message.Length is 0 or > FeedbackLimits.MessageMaxLength)
            throw new ArgumentException("The message is empty or over the limit.", nameof(submission));

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(_timeout);

        FeedbackSendResult result;
        try
        {
            using var content = new ByteArrayContent(BuildBody(submission, message));
            content.Headers.ContentType = JsonContentType;
            using var request = new HttpRequestMessage(HttpMethod.Post, _endpoint) { Content = content };

            using var response = await _http.SendAsync(request, timeout.Token).ConfigureAwait(false);
            var body = await response.Content.ReadAsStringAsync(timeout.Token).ConfigureAwait(false);

            result = Classify((int)response.StatusCode, ParseBody(body));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            result = new FeedbackSendResult(FeedbackSendOutcome.Timeout);
        }
        catch (Exception ex) when (ex is HttpRequestException or IOException)
        {
            _logger.LogWarning("Feedback not sent: no connection ({Error})", ex.Message);
            return new FeedbackSendResult(FeedbackSendOutcome.NetworkError);
        }

        Log(result);
        return result;
    }

    public void Dispose() => _http.Dispose();

    /// <summary>
    /// Maps a response to an outcome on status <em>and</em> error code, row by row from
    /// the contract's table. Anything not in the table — including a known status with an
    /// unknown or missing error, such as a proxy's HTML error page — is a server error,
    /// which the dialog answers with the fallback.
    /// </summary>
    internal static FeedbackSendResult Classify(int status, ResponseBody? body)
    {
        var error = body?.Error;
        var field = body?.Field;

        var outcome = (status, error) switch
        {
            // Success needs the number the contract promises; "ok" without a usable id
            // is not the API talking, so it takes the fallback like any other oddity.
            (201 or 200, _) when body is { Ok: true, Id: not null } => FeedbackSendOutcome.Sent,
            (400, "invalid_field") when field == "email" => FeedbackSendOutcome.InvalidEmail,
            (400, "invalid_field" or "invalid_json") => FeedbackSendOutcome.Rejected,
            (403, "turnstile_failed") => FeedbackSendOutcome.Rejected,
            (405, "method_not_allowed") => FeedbackSendOutcome.Rejected,
            (413, "payload_too_large") => FeedbackSendOutcome.Rejected,
            (415, "unsupported_media_type") => FeedbackSendOutcome.Rejected,
            (429, "rate_limited") => FeedbackSendOutcome.RateLimited,
            (429, "daily_limit") => FeedbackSendOutcome.AppPathClosed,
            (503, "app_paused") => FeedbackSendOutcome.AppPathClosed,
            _ => FeedbackSendOutcome.ServerError,
        };

        return new FeedbackSendResult(
            outcome,
            status,
            error,
            field,
            outcome == FeedbackSendOutcome.Sent ? body?.Id : null,
            outcome == FeedbackSendOutcome.Sent && body?.Duplicate == true);
    }

    /// <summary>
    /// The request body. Optional values that are null or blank are left out, which the
    /// contract treats as "not given" — so an unticked field is simply absent.
    /// </summary>
    internal static byte[] BuildBody(FeedbackSubmission submission, string normalizedMessage)
    {
        using var stream = new MemoryStream();
        using (var json = new Utf8JsonWriter(stream))
        {
            json.WriteStartObject();
            json.WriteString("kind", submission.Kind == FeedbackKind.Bug ? "bug" : "feedback");
            json.WriteString("source", "app");
            json.WriteString("message", normalizedMessage);
            WriteOptional(json, "email", submission.Email);
            WriteOptional(json, "appVersion", submission.AppVersion);
            WriteOptional(json, "os", submission.OperatingSystem);
            WriteOptional(json, "uiLanguage", submission.UiLanguage);
            json.WriteEndObject();
        }

        return stream.ToArray();
    }

    internal static Uri ResolveEndpoint(Func<string, string?> environment)
    {
#if DEBUG
        var value = environment(EndpointOverrideVariable);
        if (Uri.TryCreate(value, UriKind.Absolute, out var overridden)
            && (overridden.Scheme == Uri.UriSchemeHttps || overridden.Scheme == Uri.UriSchemeHttp))
        {
            return overridden;
        }
#endif
        _ = environment;
        return FeedbackLinks.Endpoint;
    }

    internal static ResponseBody? ParseBody(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            using var document = JsonDocument.Parse(text);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return null;

            return new ResponseBody(
                Ok: root.TryGetProperty("ok", out var ok) && ok.ValueKind == JsonValueKind.True,
                // TryGetInt64 throws on a non-number, so check the kind first: a proxy
                // answering {"id":"x"} must become a failed result, not an exception.
                Id: root.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number && id.TryGetInt64(out var number)
                    ? number
                    : null,
                Duplicate: root.TryGetProperty("duplicate", out var duplicate) && duplicate.ValueKind == JsonValueKind.True,
                Error: StringOrNull(root, "error"),
                Field: StringOrNull(root, "field"));
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? StringOrNull(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static void WriteOptional(Utf8JsonWriter json, string name, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
            json.WriteString(name, value.Trim());
    }

    private static FeedbackDiagnostics ReadDiagnostics()
    {
        // -p:Version in release.yml lands here, e.g. "0.6.0+<commit>".
        var version = Assembly.GetEntryAssembly()?
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion;

        return new FeedbackDiagnostics(
            FeedbackLimits.NormalizeAppVersion(version),
            OperatingSystemDescription.Current());
    }

    // Never the message or the email — only what the server said about them.
    private void Log(FeedbackSendResult result)
    {
        switch (result.Outcome)
        {
            case FeedbackSendOutcome.Sent:
                _logger.LogInformation(
                    "Feedback sent: #{Id} (HTTP {Status}{Duplicate})",
                    result.SubmissionId, result.StatusCode, result.IsDuplicate ? ", duplicate" : "");
                break;
            case FeedbackSendOutcome.Rejected:
                // The contract calls every one of these a bug in the app.
                _logger.LogError(
                    "Feedback rejected as malformed: HTTP {Status}, error {Error}, field {Field}",
                    result.StatusCode, result.ErrorCode ?? "(none)", result.Field ?? "(none)");
                break;
            case FeedbackSendOutcome.Timeout:
                _logger.LogWarning("Feedback not sent: no answer within {Timeout}", _timeout);
                break;
            default:
                _logger.LogWarning(
                    "Feedback not sent: {Outcome} (HTTP {Status}, error {Error}, field {Field})",
                    result.Outcome, result.StatusCode, result.ErrorCode ?? "(none)", result.Field ?? "(none)");
                break;
        }
    }

    /// <summary>The parts of the API's JSON answer the classification reads.</summary>
    internal sealed record ResponseBody(bool Ok, long? Id, bool Duplicate, string? Error, string? Field);
}
