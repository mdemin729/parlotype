namespace Parlotype.Core.Speech;

/// <summary>Why a configured cloud base URL was refused.</summary>
public enum CloudBaseUrlError
{
    /// <summary>Not parseable as an absolute URL at all.</summary>
    NotAbsoluteUrl,

    /// <summary>Plain <c>http</c> to something other than loopback.</summary>
    PlainHttpNotLoopback,

    /// <summary>A scheme that is neither <c>https</c> nor <c>http</c>.</summary>
    UnsupportedScheme
}

/// <summary>
/// A rejected base URL: the reason as an identity the UI can translate, plus
/// the offending scheme when there is one (ADR-064 amendment).
/// </summary>
public readonly record struct CloudBaseUrlFailure(CloudBaseUrlError Error, string? Scheme = null)
{
    /// <summary>
    /// Invariant English form, no trailing period, for embedding in log lines
    /// and exception messages. The window shows the translated form instead.
    /// </summary>
    public string Description => Error switch
    {
        CloudBaseUrlError.NotAbsoluteUrl => "not a valid absolute URL",
        CloudBaseUrlError.PlainHttpNotLoopback =>
            "plain http is only allowed for localhost — the API key and audio would leave this machine unencrypted",
        CloudBaseUrlError.UnsupportedScheme => $"unsupported scheme '{Scheme}' — use https",
        _ => Error.ToString(),
    };
}

/// <summary>
/// Validates user-configured cloud provider base URLs: HTTPS is required for
/// any non-loopback host, because the request carries the bearer API key and
/// recorded audio (security audit 2026-07-11, S3). Plain HTTP stays allowed
/// for loopback so self-hosted OpenAI-compatible servers (LM Studio,
/// llama.cpp) keep working. Shared by the cloud recognizers (fail at
/// initialisation) and the Cloud providers settings page (inline hint at
/// save time).
/// </summary>
public static class CloudBaseUrlValidator
{
    /// <summary>
    /// Returns true when <paramref name="baseUrl"/> is acceptable. Null/blank
    /// is valid — it means "use the provider's default", which is HTTPS.
    /// On failure <paramref name="failure"/> carries the reason.
    /// </summary>
    public static bool TryValidate(string? baseUrl, out CloudBaseUrlFailure? failure)
    {
        failure = null;

        if (string.IsNullOrWhiteSpace(baseUrl))
            return true;

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            failure = new CloudBaseUrlFailure(CloudBaseUrlError.NotAbsoluteUrl);
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            if (uri.IsLoopback)
                return true;

            failure = new CloudBaseUrlFailure(CloudBaseUrlError.PlainHttpNotLoopback);
            return false;
        }

        failure = new CloudBaseUrlFailure(CloudBaseUrlError.UnsupportedScheme, uri.Scheme);
        return false;
    }
}
