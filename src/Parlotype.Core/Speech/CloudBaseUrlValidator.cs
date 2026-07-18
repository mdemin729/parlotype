namespace Parlotype.Core.Speech;

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
    /// On failure <paramref name="error"/> carries a short human-readable
    /// reason (no trailing period) for embedding in longer messages.
    /// </summary>
    public static bool TryValidate(string? baseUrl, out string? error)
    {
        error = null;

        if (string.IsNullOrWhiteSpace(baseUrl))
            return true;

        if (!Uri.TryCreate(baseUrl.Trim(), UriKind.Absolute, out var uri))
        {
            error = "not a valid absolute URL";
            return false;
        }

        if (uri.Scheme == Uri.UriSchemeHttps)
            return true;

        if (uri.Scheme == Uri.UriSchemeHttp)
        {
            if (uri.IsLoopback)
                return true;

            error = "plain http is only allowed for localhost — the API key and audio would leave this machine unencrypted";
            return false;
        }

        error = $"unsupported scheme '{uri.Scheme}' — use https";
        return false;
    }
}
