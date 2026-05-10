using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Delegating <see cref="ISpeechRecognizer"/> that resolves the actual recognizer
/// (Whisper or LlamaCpp) based on settings at <see cref="InitializeAsync"/> time.
/// Registered as the <see cref="ISpeechRecognizer"/> singleton in DI.
/// </summary>
public sealed class DelegatingSpeechRecognizer : ISpeechRecognizer
{
    private readonly SpeechRecognizerFactory _factory;
    private readonly ILogger<DelegatingSpeechRecognizer> _logger;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private ISpeechRecognizer? _inner;
    private bool _disposed;

    public bool IsReady => _inner?.IsReady ?? false;

    public DelegatingSpeechRecognizer(
        SpeechRecognizerFactory factory,
        ILogger<DelegatingSpeechRecognizer> logger)
    {
        _factory = factory;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _inner = await _factory.GetRecognizerAsync(cancellationToken);
            await _inner.InitializeAsync(cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task InitializeAsync(WhisperOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _inner = await _factory.GetRecognizerAsync(cancellationToken);
            await _inner.InitializeAsync(options, cancellationToken);
        }
        finally
        {
            _lock.Release();
        }
    }

    public Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var inner = _inner ?? throw new InvalidOperationException(
            "Recognizer is not initialized. Call InitializeAsync first.");

        return inner.TranscribeAsync(samples, cancellationToken);
    }

    public async Task UnloadAsync()
    {
        await _lock.WaitAsync();
        try
        {
            if (_inner is not null)
            {
                await _inner.UnloadAsync();
                _inner = null;
            }
        }
        finally
        {
            _lock.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;

        if (_inner is not null)
            await _inner.DisposeAsync();
    }
}
