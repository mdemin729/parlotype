namespace Parlotype.Core.Speech;

/// <summary>
/// Classifies why a cloud transcription request failed, so the UI can react
/// appropriately (route to key settings vs. "try again later") without
/// string-matching provider messages (ADR-043 amendment).
/// </summary>
public enum CloudSpeechErrorKind
{
    /// <summary>HTTP 401/403 — the provider rejected the API key.</summary>
    KeyRejected,

    /// <summary>HTTP 429 with a quota/billing error code — credits or monthly budget exhausted.</summary>
    QuotaExceeded,

    /// <summary>HTTP 429 without a quota code — requests are arriving too fast; transient.</summary>
    RateLimited,

    /// <summary>HTTP 5xx — provider-side trouble; transient.</summary>
    ProviderUnavailable,

    /// <summary>Anything else the provider returned.</summary>
    Other,
}

/// <summary>
/// Thrown when a cloud transcription HTTP request fails. Carries a
/// user-presentable <see cref="Exception.Message"/> (the provider's error
/// envelope is parsed — e.g. OpenAI's <c>{"error":{"message":…,"code":…}}</c> —
/// rather than dumped raw), plus the failure <see cref="Kind"/> and provider
/// name so the UI can offer the right next step. Derives from
/// <see cref="InvalidOperationException"/> so generic failure handling keeps
/// working (same pattern as <see cref="CloudProviderNotConfiguredException"/>).
/// </summary>
public sealed class CloudSpeechTranscriptionException : InvalidOperationException
{
    /// <summary>What went wrong, classified from the HTTP status and provider error code.</summary>
    public CloudSpeechErrorKind Kind { get; }

    /// <summary>Display name of the provider that failed (e.g. "OpenAI-compatible provider").</summary>
    public string Provider { get; }

    public CloudSpeechTranscriptionException(CloudSpeechErrorKind kind, string provider, string message)
        : base(message)
    {
        Kind = kind;
        Provider = provider;
    }
}
