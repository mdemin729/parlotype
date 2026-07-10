using System.Net;
using Microsoft.Extensions.Logging;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Builds the exception thrown when a cloud transcription HTTP request fails.
/// Shared by <see cref="OpenAiCompatibleSpeechRecognizer"/> and
/// <see cref="XaiGrokSpeechRecognizer"/> so both providers report failures the
/// same way: the body is logged (never the request's Authorization header —
/// callers must not pass it in), 401/403 are special-cased as a rejected key,
/// and everything else surfaces the HTTP status plus a trimmed provider
/// message.
/// </summary>
internal static class CloudSpeechHttpError
{
    private const int MaxMessageLength = 500;

    /// <summary>Reads the response body, logs it, and returns the exception to throw. Never throws itself.</summary>
    internal static async Task<InvalidOperationException> BuildAsync(
        HttpResponseMessage response,
        string providerDisplayName,
        ILogger logger,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        logger.LogWarning(
            "{Provider} transcription request failed (HTTP {Status}): {Body}",
            providerDisplayName, (int)response.StatusCode, body);

        var trimmed = body.Length > MaxMessageLength ? body[..MaxMessageLength] + "…" : body;

        return response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden
            ? new InvalidOperationException(
                $"API key rejected by provider ({providerDisplayName}, HTTP {(int)response.StatusCode}): {trimmed}")
            : new InvalidOperationException(
                $"{providerDisplayName} transcription failed (HTTP {(int)response.StatusCode}): {trimmed}");
    }
}
