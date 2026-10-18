namespace Parlotype.Core.Feedback;

/// <summary>
/// Sends one feedback message or bug report to parlotype.dev (ADR-070).
/// </summary>
/// <remarks>
/// <para>
/// This is a network request, and Parlotype documents every one it makes, so the rules
/// are part of the contract: a send happens only when the user presses Send — never
/// automatically, in the background, on a crash or later from a queue — and it is exactly
/// one HTTPS request. Implementations never retry, never attach logs, audio, transcripts,
/// settings or keys, and send no identifier or header that carries information about the
/// user. Only the values in <see cref="FeedbackSubmission"/> leave the machine.
/// </para>
/// <para>
/// Failures are results, not exceptions: <see cref="SendAsync"/> answers with a
/// <see cref="FeedbackSendResult"/> for every response, timeout and connection failure,
/// and throws only when the caller cancels.
/// </para>
/// </remarks>
public interface IFeedbackService
{
    /// <summary>
    /// The version and OS description the dialog offers to include. Computed once; cheap to
    /// read.
    /// </summary>
    FeedbackDiagnostics Diagnostics { get; }

    /// <summary>Sends <paramref name="submission"/> once and classifies the answer.</summary>
    /// <exception cref="OperationCanceledException"><paramref name="cancellationToken"/> was cancelled.</exception>
    Task<FeedbackSendResult> SendAsync(FeedbackSubmission submission, CancellationToken cancellationToken = default);
}
