using Parlotype.Core.Feedback;

namespace Parlotype.Desktop.Tests.Mocks;

/// <summary>
/// Records submissions and answers with a scripted result. Never touches the network —
/// a real send would land in the developer's inbox.
/// </summary>
public sealed class MockFeedbackService : IFeedbackService
{
    public FeedbackDiagnostics Diagnostics { get; set; } = new("0.6.0", "Windows 11 24H2 (10.0.26100)");

    /// <summary>What every send answers. Defaults to a stored submission #42.</summary>
    public FeedbackSendResult Result { get; set; } = new(FeedbackSendOutcome.Sent, 201, SubmissionId: 42);

    /// <summary>When set, a send waits for it — lets a test hold a request in flight.</summary>
    public TaskCompletionSource? Gate { get; set; }

    public List<FeedbackSubmission> Submissions { get; } = [];

    public async Task<FeedbackSendResult> SendAsync(FeedbackSubmission submission, CancellationToken cancellationToken = default)
    {
        Submissions.Add(submission);

        if (Gate is not null)
            await Gate.Task;

        return Result;
    }
}
