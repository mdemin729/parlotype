namespace Parlotype.Core.Feedback;

/// <summary>
/// The machine-derived values the feedback dialog offers to include (ADR-070). Shown to the
/// user verbatim before sending; each one can be unticked.
/// </summary>
/// <param name="AppVersion">The running version, already in the API's alphabet and length.</param>
/// <param name="OperatingSystem">One line describing the OS, e.g. <c>Windows 11 24H2 (10.0.26100)</c>.</param>
public sealed record FeedbackDiagnostics(string AppVersion, string OperatingSystem);
