namespace Parlotype.Core.TextProcessing;

/// <summary>What stands between the user and a working cleanup pass.</summary>
public enum SpeechCleanupReadiness
{
    /// <summary>The model file and a <c>llama-server</c> are both on disk.</summary>
    Ready,

    /// <summary>The cleanup model has not been downloaded.</summary>
    ModelMissing,

    /// <summary>No usable <c>llama-server</c> is installed or selected.</summary>
    ServerMissing,
}

/// <summary>
/// Rewrites raw recognizer output as clean written text with a local model
/// (ADR-073). Runs after any speech engine; it never sees audio.
/// </summary>
public interface ISpeechCleanupService
{
    /// <summary>
    /// Checks what is on disk. Starts nothing and makes no network request.
    /// </summary>
    Task<SpeechCleanupReadiness> GetReadinessAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Loads the model so the first <see cref="CleanAsync"/> does not pay for
    /// it. Idempotent. Throws when the model or the server is missing or the
    /// server fails to start.
    /// </summary>
    Task PrepareAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns <paramref name="transcript"/> rewritten per
    /// <paramref name="options"/>. An empty result is valid: a transcript that
    /// was only filler cleans to nothing. Throws on any failure; the caller
    /// decides what to do with the raw text.
    /// </summary>
    Task<string> CleanAsync(
        string transcript,
        SpeechCleanupOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the model process and frees its memory. The service stays usable:
    /// the next <see cref="PrepareAsync"/> or <see cref="CleanAsync"/> loads it again.
    /// </summary>
    Task UnloadAsync();
}
