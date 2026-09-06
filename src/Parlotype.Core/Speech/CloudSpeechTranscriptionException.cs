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
/// Thrown when a cloud transcription HTTP request fails. Carries the failure
/// <see cref="Kind"/>, the engine and provider name, the HTTP status, and the
/// provider's own parsed error text (its error envelope — e.g. OpenAI's
/// <c>{"error":{"message":…,"code":…}}</c> — rather than the raw body). Derives
/// from <see cref="InvalidOperationException"/> so generic failure handling
/// keeps working (same pattern as <see cref="CloudProviderNotConfiguredException"/>).
/// </summary>
/// <remarks>
/// <see cref="Exception.Message"/> is the invariant English form the logs
/// write. The status text and dialog compose their own sentence from the parts
/// below so it can be translated (ADR-064 amendment) — everything but
/// <see cref="ProviderMessage"/>, which is the provider's wording and stays as
/// it arrived.
/// </remarks>
public sealed class CloudSpeechTranscriptionException : InvalidOperationException
{
    /// <summary>What went wrong, classified from the HTTP status and provider error code.</summary>
    public CloudSpeechErrorKind Kind { get; }

    /// <summary>The cloud engine that failed.</summary>
    public SpeechEngine Engine { get; }

    /// <summary>Invariant display name of the provider that failed (e.g. "OpenAI-compatible provider").</summary>
    public string Provider { get; }

    /// <summary>The HTTP status the provider returned, or 0 if there was none.</summary>
    public int StatusCode { get; }

    /// <summary>The provider's own error text, already parsed out of its envelope and length-capped.</summary>
    public string? ProviderMessage { get; }

    public CloudSpeechTranscriptionException(
        CloudSpeechErrorKind kind,
        SpeechEngine engine,
        string provider,
        string message,
        int statusCode = 0,
        string? providerMessage = null)
        : base(message)
    {
        Kind = kind;
        Engine = engine;
        Provider = provider;
        StatusCode = statusCode;
        ProviderMessage = providerMessage;
    }
}
