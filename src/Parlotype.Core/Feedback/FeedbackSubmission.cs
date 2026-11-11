namespace Parlotype.Core.Feedback;

/// <summary>
/// One message for <see cref="IFeedbackService.SendAsync"/> — exactly what the user saw in
/// the dialog and nothing more (ADR-070).
/// </summary>
/// <remarks>
/// Every optional value is <see langword="null"/> when the user left it out: an empty email
/// box, or an unticked diagnostic. A null value is omitted from the request entirely, so
/// what the dialog shows as "will be sent" is what goes over the wire.
/// </remarks>
/// <param name="Kind">Feedback or bug report.</param>
/// <param name="Message">The user's text, at most <see cref="FeedbackLimits.MessageMaxLength"/> characters.</param>
/// <param name="Email">Optional reply address.</param>
/// <param name="AppVersion">Optional — the running version, e.g. <c>0.6.0</c>.</param>
/// <param name="OperatingSystem">Optional — e.g. <c>Windows 11 24H2 (10.0.26100)</c>.</param>
/// <param name="UiLanguage">Optional — the interface language, a neutral culture name such as <c>de</c>.</param>
public sealed record FeedbackSubmission(
    FeedbackKind Kind,
    string Message,
    string? Email = null,
    string? AppVersion = null,
    string? OperatingSystem = null,
    string? UiLanguage = null);
