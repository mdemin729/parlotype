using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Whisper.net;
using Whisper.net.LibraryLoader;

namespace Parlotype.Platform.Speech;

/// <summary>Speech recognition using Whisper.net with automatic model download.</summary>
public sealed class WhisperSpeechRecognizer : ISpeechRecognizer
{
    private readonly IModelDownloadService _downloadService;
    private readonly ISettingsService _settings;
    private readonly ILogger<WhisperSpeechRecognizer> _logger;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private bool _disposed;

    public bool IsReady { get; private set; }

    public WhisperSpeechRecognizer(IModelDownloadService downloadService, ISettingsService settings, ILogger<WhisperSpeechRecognizer> logger)
    {
        _downloadService = downloadService;
        _settings = settings;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsReady)
            return;

        var savedModel = await _settings.GetAsync<string>(SettingsKeys.SelectedWhisperModel, cancellationToken);
        var modelType = Enum.TryParse<WhisperModelType>(savedModel, out var parsed)
            ? parsed
            : WhisperModelType.Base;

        _logger.LogInformation("Initializing Whisper with model type: {ModelType}", modelType);
        var modelPath = await _downloadService.EnsureModelAsync(modelType, cancellationToken);

        await WhisperRuntimeBootstrap.EnsureInitializedAsync(_settings, _logger);

        _factory = WhisperFactory.FromPath(modelPath);
        _logger.LogInformation("Whisper runtime loaded: {Runtime}", WhisperRuntimeBootstrap.LoadedRuntime?.ToString() ?? "unknown");

        _processor = _factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        IsReady = true;
        _logger.LogInformation("Whisper model loaded successfully");
    }

    public async Task InitializeAsync(WhisperOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsReady)
            return;

        _logger.LogInformation(
            "Initializing Whisper with model: {Model}, language: {Language}, beamSize: {BeamSize}, temperature: {Temperature}",
            options.Model, options.Language, options.BeamSize, options.Temperature);

        var modelPath = await _downloadService.EnsureModelAsync(options.Model, cancellationToken);

        WhisperRuntimeBootstrap.Initialize(options.RuntimePreference, _logger);

        _factory = WhisperFactory.FromPath(modelPath);
        _logger.LogInformation("Whisper runtime loaded: {Runtime}", WhisperRuntimeBootstrap.LoadedRuntime?.ToString() ?? "unknown");

        var builder = _factory.CreateBuilder()
            .WithLanguage(options.Language)
            .WithTemperature(options.Temperature);

        if (options.Threads is not null)
            builder.WithThreads(options.Threads.Value);

        if (options.BeamSize > 1)
        {
            var beamStrategy = (BeamSearchSamplingStrategyBuilder)builder.WithBeamSearchSamplingStrategy();
            beamStrategy.WithBeamSize(options.BeamSize);
        }
        else
        {
            builder.WithGreedySamplingStrategy();
        }

        if (!string.IsNullOrEmpty(options.InitialPrompt))
            builder.WithPrompt(options.InitialPrompt);

        _processor = builder.Build();
        IsReady = true;
        _logger.LogInformation("Whisper model loaded successfully");
    }

    public async Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsReady || _processor is null)
            throw new InvalidOperationException("Speech recognizer is not initialized. Call InitializeAsync first.");

        var segments = new List<SegmentData>();
        await foreach (var segment in _processor.ProcessAsync(samples, cancellationToken))
        {
            segments.Add(segment);
        }

        var text = string.Join(" ", segments.Select(s => s.Text)).Trim();
        var avgConfidence = segments.Count > 0
            ? segments.Average(s => s.Probability)
            : 0.0;
        var language = segments.FirstOrDefault()?.Language;

        _logger.LogDebug("Transcription completed: {SegmentCount} segments, confidence: {Confidence:F2}", segments.Count, avgConfidence);

        return new TranscriptionResult
        {
            Text = text,
            Confidence = avgConfidence,
            DetectedLanguage = language
        };
    }

    public async Task UnloadAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsReady)
            return;

        if (_processor is not null)
        {
            await _processor.DisposeAsync();
            _processor = null;
        }

        if (_factory is not null)
        {
            _factory.Dispose();
            _factory = null;
        }

        IsReady = false;
        _logger.LogInformation("Whisper model unloaded");
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        // Unload before setting _disposed so UnloadAsync doesn't throw
        await UnloadAsync().ConfigureAwait(false);
        _disposed = true;
    }
}
