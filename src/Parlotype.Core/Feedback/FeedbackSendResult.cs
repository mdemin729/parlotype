namespace Parlotype.Core.Feedback;

/// <summary>
/// How a send ended, decided from the HTTP status <em>and</em> the body's <c>error</c>
/// code together, as the feedback API contract requires (ADR-070).
/// </summary>
public enum FeedbackSendOutcome
{
    /// <summary>201, or 200 with <c>duplicate</c>: the message is stored.</summary>
    Sent,

    /// <summary>400 <c>invalid_field</c> on <c>email</c>: the user has to fix the address.</summary>
    InvalidEmail,

    /// <summary>429 <c>rate_limited</c>: too many sends from this network in a minute.</summary>
    RateLimited,

    /// <summary>
    /// The server refused the request as malformed (400 other than email, 403, 405, 413,
    /// 415). A bug in this build, not something the user can fix.
    /// </summary>
    Rejected,

    /// <summary>
    /// The app path is closed for now: 429 <c>daily_limit</c> or 503 <c>app_paused</c>.
    /// The website form still works.
    /// </summary>
    AppPathClosed,

    /// <summary>500, 503 <c>unavailable</c>, any other status, or a body that is not the API's JSON.</summary>
    ServerError,

    /// <summary>No answer within the timeout.</summary>
    Timeout,

    /// <summary>No connection: offline, DNS, TLS or a refused socket.</summary>
    NetworkError,
}

/// <summary>The result of one <see cref="IFeedbackService.SendAsync"/> call.</summary>
/// <param name="Outcome">What the dialog should do next.</param>
/// <param name="StatusCode">The HTTP status, when a response arrived.</param>
/// <param name="ErrorCode">The body's <c>error</c>, when there was one — for the log.</param>
/// <param name="Field">The body's <c>field</c> on <c>invalid_field</c> — for the log.</param>
/// <param name="SubmissionId">The stored submission's number on success.</param>
/// <param name="IsDuplicate">True when the server already had this text from the last hour.</param>
public sealed record FeedbackSendResult(
    FeedbackSendOutcome Outcome,
    int? StatusCode = null,
    string? ErrorCode = null,
    string? Field = null,
    long? SubmissionId = null,
    bool IsDuplicate = false)
{
    /// <summary>True only when the server confirmed the message is stored.</summary>
    public bool IsSent => Outcome == FeedbackSendOutcome.Sent;

    /// <summary>
    /// True when the user should be offered the website form and GitHub Issues: the app
    /// cannot deliver this message now, and pressing Send again will not help.
    /// </summary>
    public bool OffersFallback => Outcome is FeedbackSendOutcome.Rejected
        or FeedbackSendOutcome.AppPathClosed
        or FeedbackSendOutcome.ServerError
        or FeedbackSendOutcome.Timeout
        or FeedbackSendOutcome.NetworkError;
}
