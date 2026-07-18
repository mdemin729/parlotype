using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Builds the exception thrown when a cloud transcription HTTP request fails.
/// Shared by <see cref="OpenAiCompatibleSpeechRecognizer"/> and
/// <see cref="XaiGrokSpeechRecognizer"/> so both providers report failures the
/// same way. The full response body is logged (never the request's
/// Authorization header — callers must not pass it in); the exception message
/// is built from the provider's parsed error envelope (OpenAI's
/// <c>{"error":{"message":…,"code":…}}</c> and common variants) rather than the
/// raw JSON dump, and classified into a <see cref="CloudSpeechErrorKind"/> per
/// the provider error-code tables (401/403 key, 429 quota vs. rate limit, 5xx
/// availability) so the UI can suggest the right fix.
/// </summary>
internal static class CloudSpeechHttpError
{
    private const int MaxMessageLength = 500;

    /// <summary>Reads the response body, logs it, and returns the exception to throw. Never throws itself.</summary>
    internal static async Task<CloudSpeechTranscriptionException> BuildAsync(
        HttpResponseMessage response,
        string providerDisplayName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        // Cap the provider-controlled body before it reaches persistent logs
        // (security audit 2026-07-11, S1) — the parsed envelope below carries
        // the useful part anyway.
        logger.LogWarning(
            "{Provider} transcription request failed (HTTP {Status}): {Body}",
            providerDisplayName, (int)response.StatusCode, Trim(body));

        var (providerMessage, errorCode) = ParseErrorEnvelope(body);
        providerMessage ??= Trim(body);

        var status = (int)response.StatusCode;
        var kind = Classify(response.StatusCode, errorCode, providerMessage);

        var message = kind switch
        {
            CloudSpeechErrorKind.KeyRejected =>
                $"{providerDisplayName} rejected the API key (HTTP {status}): {providerMessage} " +
                "Check the key in Settings → Speech engine.",
            CloudSpeechErrorKind.QuotaExceeded =>
                $"{providerDisplayName}: API quota exceeded — check your plan and billing with the provider. " +
                $"({providerMessage})",
            CloudSpeechErrorKind.RateLimited =>
                $"{providerDisplayName}: rate limit reached — wait a moment and try again. ({providerMessage})",
            CloudSpeechErrorKind.ProviderUnavailable =>
                $"{providerDisplayName} is unavailable right now (HTTP {status}) — try again shortly. " +
                $"({providerMessage})",
            _ =>
                $"{providerDisplayName} transcription failed (HTTP {status}): {providerMessage}",
        };

        return new CloudSpeechTranscriptionException(kind, providerDisplayName, message);
    }

    /// <summary>
    /// Extracts the human-readable message and machine error code from a
    /// provider error body. Understands OpenAI's envelope
    /// (<c>{"error":{"message":…,"type":…,"code":…}}</c>), an <c>error</c>
    /// that is a plain string, and a top-level <c>message</c>. Returns
    /// <c>(null, null)</c> for non-JSON or unrecognized shapes.
    /// </summary>
    private static (string? Message, string? Code) ParseErrorEnvelope(string body)
    {
        if (string.IsNullOrWhiteSpace(body))
            return (null, null);

        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                return (null, null);

            if (root.TryGetProperty("error", out var error))
            {
                if (error.ValueKind == JsonValueKind.String)
                    return (Trim(error.GetString()), null);

                if (error.ValueKind == JsonValueKind.Object)
                {
                    var message = error.TryGetProperty("message", out var m) && m.ValueKind == JsonValueKind.String
                        ? m.GetString()
                        : null;
                    // Prefer "code", fall back to "type" — OpenAI populates both
                    // with the same value for quota errors; other providers use one.
                    var code = error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.String
                        ? c.GetString()
                        : null;
                    code ??= error.TryGetProperty("type", out var t) && t.ValueKind == JsonValueKind.String
                        ? t.GetString()
                        : null;
                    return (Trim(message), code);
                }
            }

            if (root.TryGetProperty("message", out var topMessage) && topMessage.ValueKind == JsonValueKind.String)
                return (Trim(topMessage.GetString()), null);

            return (null, null);
        }
        catch (JsonException)
        {
            return (null, null);
        }
    }

    private static CloudSpeechErrorKind Classify(HttpStatusCode status, string? errorCode, string providerMessage)
    {
        if (status is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            return CloudSpeechErrorKind.KeyRejected;

        if (status == HttpStatusCode.TooManyRequests)
        {
            // OpenAI signals billing exhaustion as 429 code "insufficient_quota";
            // fall back to sniffing the message for providers without codes.
            var isQuota = string.Equals(errorCode, "insufficient_quota", StringComparison.OrdinalIgnoreCase)
                || providerMessage.Contains("quota", StringComparison.OrdinalIgnoreCase)
                || providerMessage.Contains("billing", StringComparison.OrdinalIgnoreCase);
            return isQuota ? CloudSpeechErrorKind.QuotaExceeded : CloudSpeechErrorKind.RateLimited;
        }

        if ((int)status >= 500)
            return CloudSpeechErrorKind.ProviderUnavailable;

        return CloudSpeechErrorKind.Other;
    }

    private static string Trim(string? text)
    {
        text = (text ?? string.Empty).Trim();
        return text.Length > MaxMessageLength ? text[..MaxMessageLength] + "…" : text;
    }
}
