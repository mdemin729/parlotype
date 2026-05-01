namespace Parlotype.Core.Speech;

/// <summary>
/// Converts audio data into text using a speech recognition engine.
/// </summary>
public interface ISpeechRecognizer : IAsyncDisposable
{
    /// <summary>Loads the speech recognition model.</summary>
    Task InitializeAsync(CancellationToken cancellationToken = default);

    /// <summary>Loads the speech recognition model with explicit options.</summary>
    Task InitializeAsync(WhisperOptions options, CancellationToken cancellationToken = default)
        => InitializeAsync(cancellationToken);

    /// <summary>Transcribes the provided audio samples (mono float in [-1, 1]) into text.</summary>
    Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unloads the current model, releasing resources so the next
    /// <see cref="InitializeAsync(CancellationToken)"/> call reloads from settings.
    /// Unlike <see cref="IAsyncDisposable.DisposeAsync"/>, the recognizer remains
    /// usable after unloading.
    /// </summary>
    Task UnloadAsync() => Task.CompletedTask;

    /// <summary>Whether the model has been loaded and is ready.</summary>
    bool IsReady { get; }
}
