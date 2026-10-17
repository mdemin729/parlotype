using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Feedback;
using Parlotype.Platform.Feedback;
using Xunit;

namespace Parlotype.Tests.Feedback;

/// <summary>
/// <see cref="HttpFeedbackService"/> against a fake <see cref="HttpMessageHandler"/>.
/// Nothing here reaches parlotype.dev: every message lands in the developer's inbox, so a
/// test that did would be a bug report about the tests.
/// </summary>
public class HttpFeedbackServiceTests
{
    private static readonly Uri TestEndpoint = new("https://feedback.test/api/feedback");
    private static readonly FeedbackDiagnostics TestDiagnostics = new("0.6.0", "Windows 11 24H2 (10.0.26100)");

    private static HttpFeedbackService NewService(
        StubHandler handler,
        TimeSpan? timeout = null,
        ILogger? logger = null) =>
        new(TestEndpoint, handler, timeout ?? TimeSpan.FromSeconds(5), TestDiagnostics, logger);

    private static FeedbackSubmission Submission(
        string message = "The hotkey stops working after sleep.",
        string? email = null,
        string? appVersion = "0.6.0",
        string? os = "Windows 11 24H2 (10.0.26100)",
        string? uiLanguage = "de") =>
        new(FeedbackKind.Bug, message, email, appVersion, os, uiLanguage);

    // ------------------------------------------------------------ the response table

    /// <summary>
    /// One row per line of the contract's response table, plus the "anything else" rows.
    /// The outcome is decided on status and error together, so the same status appears
    /// with different answers.
    /// </summary>
    public static TheoryData<int, string, FeedbackSendOutcome, bool> ResponseTable => new()
    {
        { 201, """{"ok":true,"id":42}""", FeedbackSendOutcome.Sent, false },
        { 200, """{"ok":true,"id":42,"duplicate":true}""", FeedbackSendOutcome.Sent, false },
        { 400, """{"ok":false,"error":"invalid_json"}""", FeedbackSendOutcome.Rejected, true },
        { 400, """{"ok":false,"error":"invalid_field","field":"email"}""", FeedbackSendOutcome.InvalidEmail, false },
        { 400, """{"ok":false,"error":"invalid_field","field":"message"}""", FeedbackSendOutcome.Rejected, true },
        { 400, """{"ok":false,"error":"invalid_field","field":"body"}""", FeedbackSendOutcome.Rejected, true },
        { 403, """{"ok":false,"error":"turnstile_failed"}""", FeedbackSendOutcome.Rejected, true },
        { 405, """{"ok":false,"error":"method_not_allowed"}""", FeedbackSendOutcome.Rejected, true },
        { 413, """{"ok":false,"error":"payload_too_large"}""", FeedbackSendOutcome.Rejected, true },
        { 415, """{"ok":false,"error":"unsupported_media_type"}""", FeedbackSendOutcome.Rejected, true },
        { 429, """{"ok":false,"error":"rate_limited"}""", FeedbackSendOutcome.RateLimited, false },
        { 429, """{"ok":false,"error":"daily_limit"}""", FeedbackSendOutcome.AppPathClosed, true },
        { 500, """{"ok":false,"error":"storage_failed"}""", FeedbackSendOutcome.ServerError, true },
        { 503, """{"ok":false,"error":"app_paused"}""", FeedbackSendOutcome.AppPathClosed, true },
        { 503, """{"ok":false,"error":"unavailable"}""", FeedbackSendOutcome.ServerError, true },
        // Anything else is a server error: an unknown status, a proxy's HTML page, a
        // known status with an error the table does not list, a success without ok.
        { 502, "<html>Bad gateway</html>", FeedbackSendOutcome.ServerError, true },
        { 201, "not json", FeedbackSendOutcome.ServerError, true },
        { 200, """{"ok":false}""", FeedbackSendOutcome.ServerError, true },
        { 429, """{"ok":false,"error":"something_new"}""", FeedbackSendOutcome.ServerError, true },
        { 403, "<html>Attention required</html>", FeedbackSendOutcome.ServerError, true },
        { 301, "", FeedbackSendOutcome.ServerError, true },
    };

    [Theory]
    [MemberData(nameof(ResponseTable))]
    public async Task SendAsync_ClassifiesEachResponse(int status, string body, FeedbackSendOutcome expected, bool offersFallback)
    {
        var handler = StubHandler.Respond((HttpStatusCode)status, body);
        using var service = NewService(handler);

        var result = await service.SendAsync(Submission());

        Assert.Equal(expected, result.Outcome);
        Assert.Equal(status, result.StatusCode);
        Assert.Equal(offersFallback, result.OffersFallback);
    }

    [Fact]
    public async Task Created_ReportsTheSubmissionNumber()
    {
        using var service = NewService(StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":42}"""));

        var result = await service.SendAsync(Submission());

        Assert.True(result.IsSent);
        Assert.Equal(42, result.SubmissionId);
        Assert.False(result.IsDuplicate);
    }

    [Fact]
    public async Task Duplicate_IsASuccessWithTheFirstSubmissionsNumber()
    {
        using var service = NewService(StubHandler.Respond(HttpStatusCode.OK, """{"ok":true,"id":7,"duplicate":true}"""));

        var result = await service.SendAsync(Submission());

        Assert.True(result.IsSent);
        Assert.Equal(7, result.SubmissionId);
        Assert.True(result.IsDuplicate);
    }

    [Fact]
    public async Task InvalidField_KeepsTheFieldForTheLog()
    {
        using var service = NewService(StubHandler.Respond(HttpStatusCode.BadRequest, """{"ok":false,"error":"invalid_field","field":"os"}"""));

        var result = await service.SendAsync(Submission());

        Assert.Equal(FeedbackSendOutcome.Rejected, result.Outcome);
        Assert.Equal("invalid_field", result.ErrorCode);
        Assert.Equal("os", result.Field);
    }

    // ------------------------------------------------------------ failures without a response

    [Fact]
    public async Task NoAnswerWithinTheTimeout_IsATimeout()
    {
        var handler = new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        });
        using var service = NewService(handler, timeout: TimeSpan.FromMilliseconds(100));

        var result = await service.SendAsync(Submission());

        Assert.Equal(FeedbackSendOutcome.Timeout, result.Outcome);
        Assert.True(result.OffersFallback);
        Assert.Null(result.StatusCode);
    }

    [Fact]
    public async Task NoNetwork_IsANetworkError()
    {
        var handler = new StubHandler((_, _) =>
            throw new HttpRequestException("No such host is known.", new System.Net.Sockets.SocketException(11001)));
        using var service = NewService(handler);

        var result = await service.SendAsync(Submission());

        Assert.Equal(FeedbackSendOutcome.NetworkError, result.Outcome);
        Assert.True(result.OffersFallback);
    }

    [Fact]
    public async Task CallerCancellation_Throws_RatherThanReportingATimeout()
    {
        var handler = new StubHandler(async (_, token) =>
        {
            await Task.Delay(Timeout.Infinite, token);
            throw new InvalidOperationException("unreachable");
        });
        using var service = NewService(handler);
        using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => service.SendAsync(Submission(), cts.Token));
    }

    [Fact]
    public void DefaultTimeout_IsTheContractsFifteenSeconds()
    {
        Assert.Equal(TimeSpan.FromSeconds(15), HttpFeedbackService.DefaultTimeout);
    }

    // ------------------------------------------------------------ one request, never retried

    [Theory]
    [InlineData(500, """{"ok":false,"error":"storage_failed"}""")]
    [InlineData(429, """{"ok":false,"error":"rate_limited"}""")]
    [InlineData(503, """{"ok":false,"error":"app_paused"}""")]
    public async Task AFailure_IsNeverRetried(int status, string body)
    {
        var handler = StubHandler.Respond((HttpStatusCode)status, body);
        using var service = NewService(handler);

        await service.SendAsync(Submission());

        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task ARedirect_IsAFailure_NotASuccess()
    {
        // The production handler disables redirects; this pins that a 3xx is a failure
        // rather than a quiet send to some other address.
        var handler = StubHandler.Respond(HttpStatusCode.MovedPermanently, "");
        using var service = NewService(handler);

        var result = await service.SendAsync(Submission());

        Assert.Equal(FeedbackSendOutcome.ServerError, result.Outcome);
        Assert.Single(handler.Requests);
    }

    // ------------------------------------------------------------ what goes on the wire

    [Fact]
    public async Task Request_IsAJsonPostToTheEndpoint_WithNoIdentifyingHeaders()
    {
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await service.SendAsync(Submission());

        var request = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal(TestEndpoint, request.Uri);
        Assert.Equal("application/json; charset=utf-8", request.ContentType);
        Assert.Empty(request.UserAgent);
        Assert.False(request.HasAuthorization);
        Assert.False(request.HasCookie);
    }

    [Fact]
    public async Task Body_CarriesExactlyTheContractsFields()
    {
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await service.SendAsync(Submission(email: "  someone@example.com ", uiLanguage: "de"));

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        var root = json.RootElement;
        Assert.Equal(
            ["appVersion", "email", "kind", "message", "os", "source", "uiLanguage"],
            root.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
        Assert.Equal("bug", root.GetProperty("kind").GetString());
        Assert.Equal("app", root.GetProperty("source").GetString());
        Assert.Equal("someone@example.com", root.GetProperty("email").GetString());
        Assert.Equal("0.6.0", root.GetProperty("appVersion").GetString());
        Assert.Equal("Windows 11 24H2 (10.0.26100)", root.GetProperty("os").GetString());
        Assert.Equal("de", root.GetProperty("uiLanguage").GetString());
    }

    [Fact]
    public async Task UntickedFields_AreLeftOutOfTheBody()
    {
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await service.SendAsync(Submission(email: null, appVersion: null, os: null, uiLanguage: null));

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal(
            ["kind", "message", "source"],
            json.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task BlankEmail_IsLeftOut()
    {
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await service.SendAsync(Submission(email: "   "));

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.False(json.RootElement.TryGetProperty("email", out _));
    }

    [Fact]
    public async Task Message_IsSentNormalized()
    {
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await service.SendAsync(Submission(message: "  line one\r\nline\u0000 two\f\t end  "));

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("line one\nline two\t end", json.RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task FeedbackKind_IsSentAsFeedback()
    {
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await service.SendAsync(Submission() with { Kind = FeedbackKind.Feedback });

        using var json = JsonDocument.Parse(handler.Requests[0].Body);
        Assert.Equal("feedback", json.RootElement.GetProperty("kind").GetString());
    }

    [Fact]
    public async Task AMessageAtTheLimit_IsSent_AndFitsTheBodyCap()
    {
        // Worst case for size: every character non-ASCII, which the default JSON
        // encoder writes as \uXXXX. The contract says 5000 of them fit in 64 KB.
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await service.SendAsync(Submission(message: new string('ж', FeedbackLimits.MessageMaxLength)));

        Assert.True(Encoding.UTF8.GetByteCount(handler.Requests[0].Body) <= 64 * 1024);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \r\n\t ")]
    public async Task AnEmptyMessage_IsAProgrammingError_NotARequest(string message)
    {
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() => service.SendAsync(Submission(message: message)));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task AMessageOverTheLimit_IsAProgrammingError_NotARequest()
    {
        var handler = StubHandler.Respond(HttpStatusCode.Created, """{"ok":true,"id":1}""");
        using var service = NewService(handler);

        await Assert.ThrowsAsync<ArgumentException>(() =>
            service.SendAsync(Submission(message: new string('a', FeedbackLimits.MessageMaxLength + 1))));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task TheLog_NeverContainsTheMessageOrTheEmail()
    {
        var logger = new ListLogger();
        foreach (var (status, body) in new[]
                 {
                     (HttpStatusCode.Created, """{"ok":true,"id":1}"""),
                     (HttpStatusCode.BadRequest, """{"ok":false,"error":"invalid_field","field":"message"}"""),
                     (HttpStatusCode.ServiceUnavailable, """{"ok":false,"error":"app_paused"}"""),
                 })
        {
            using var service = NewService(StubHandler.Respond(status, body), logger: logger);
            await service.SendAsync(Submission(message: "secret words", email: "who@example.com"));
        }

        Assert.NotEmpty(logger.Lines);
        Assert.All(logger.Lines, line =>
        {
            Assert.DoesNotContain("secret words", line, StringComparison.Ordinal);
            Assert.DoesNotContain("who@example.com", line, StringComparison.Ordinal);
        });
    }

    // ------------------------------------------------------------ endpoint

    [Fact]
    public void Endpoint_IsProductionWithoutAnOverride()
    {
        Assert.Equal(FeedbackLinks.Endpoint, HttpFeedbackService.ResolveEndpoint(_ => null));
        Assert.Equal(new Uri("https://parlotype.dev/api/feedback"), FeedbackLinks.Endpoint);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("ftp://localhost/api/feedback")]
    [InlineData("file:///C:/feedback")]
    public void Endpoint_IgnoresAnOverrideThatIsNotAWebAddress(string value)
    {
        Assert.Equal(FeedbackLinks.Endpoint, HttpFeedbackService.ResolveEndpoint(_ => value));
    }

#if DEBUG
    [Fact]
    public void Endpoint_HonoursTheOverride_InDebugBuilds()
    {
        var uri = HttpFeedbackService.ResolveEndpoint(name =>
            name == HttpFeedbackService.EndpointOverrideVariable ? "http://localhost:8787/api/feedback" : null);

        Assert.Equal(new Uri("http://localhost:8787/api/feedback"), uri);
    }
#else
    [Fact]
    public void Endpoint_IgnoresTheOverride_InReleaseBuilds()
    {
        var uri = HttpFeedbackService.ResolveEndpoint(_ => "http://localhost:8787/api/feedback");

        Assert.Equal(FeedbackLinks.Endpoint, uri);
    }
#endif

    // ------------------------------------------------------------ fakes

    internal sealed record CapturedRequest(
        HttpMethod Method,
        Uri? Uri,
        string? ContentType,
        string UserAgent,
        bool HasAuthorization,
        bool HasCookie,
        string Body);

    internal sealed class StubHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> respond)
        : HttpMessageHandler
    {
        public List<CapturedRequest> Requests { get; } = [];

        public static StubHandler Respond(HttpStatusCode status, string body) =>
            new((_, _) => Task.FromResult(new HttpResponseMessage(status)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json"),
            }));

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? "" : await request.Content.ReadAsStringAsync(cancellationToken);
            Requests.Add(new CapturedRequest(
                request.Method,
                request.RequestUri,
                request.Content?.Headers.ContentType?.ToString(),
                request.Headers.UserAgent.ToString(),
                request.Headers.Authorization is not null,
                request.Headers.Contains("Cookie"),
                body));

            return await respond(request, cancellationToken);
        }
    }

    private sealed class ListLogger : ILogger
    {
        public List<string> Lines { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            Lines.Add(formatter(state, exception));
    }
}
