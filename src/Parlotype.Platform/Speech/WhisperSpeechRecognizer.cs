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
    private readonly INvidiaEnvironmentProvider _nvidia;
    private readonly IVulkanEnvironmentProvider _vulkan;
    private readonly ILogger<WhisperSpeechRecognizer> _logger;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private WhisperOptions? _currentOptions;
    private bool _disposed;

    public bool IsReady { get; private set; }

    public WhisperSpeechRecognizer(
        IModelDownloadService downloadService,
        ISettingsService settings,
        INvidiaEnvironmentProvider nvidia,
        IVulkanEnvironmentProvider vulkan,
        ILogger<WhisperSpeechRecognizer> logger)
    {
        _downloadService = downloadService;
        _settings = settings;
        _nvidia = nvidia;
        _vulkan = vulkan;
        _logger = logger;
    }

    private async Task EnsureRuntimeAvailableAsync(RuntimePreference preference, CancellationToken cancellationToken)
    {
        switch (preference)
        {
            case RuntimePreference.Cuda:
                var nvidia = await _nvidia.GetAsync(cancellationToken).ConfigureAwait(false);
                if (!nvidia.HasNvidia)
                    throw new RuntimeUnavailableException(
                        RuntimePreference.Cuda,
                        "no NVIDIA driver was detected on this machine. Install CUDA-capable drivers, switch to a different runtime in Settings, or use Auto.");
                break;
            case RuntimePreference.Vulkan:
                var vulkan = await _vulkan.GetAsync(cancellationToken).ConfigureAwait(false);
                if (!vulkan.HasVulkanLoader)
                    throw new RuntimeUnavailableException(
                        RuntimePreference.Vulkan,
                        "the Vulkan loader (vulkan-1.dll) was not found. Install your GPU vendor's latest drivers or the Vulkan SDK from https://vulkan.lunarg.com/sdk/home, then restart Parlotype.");
                break;
        }
    }

    private void AssertLoadedRuntimeMatches(RuntimePreference preference)
    {
        if (preference is not (RuntimePreference.Cuda or RuntimePreference.Vulkan))
            return;

        var loaded = WhisperRuntimeBootstrap.LoadedRuntime;
        var expected = preference == RuntimePreference.Cuda ? RuntimeLibrary.Cuda : RuntimeLibrary.Vulkan;
        if (loaded != expected)
        {
            throw new RuntimeUnavailableException(
                preference,
                $"Whisper.net loaded '{loaded?.ToString() ?? "(none)"}' instead of '{expected}'. The native runtime may be missing or incompatible.");
        }
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

        var savedRuntime = await _settings.GetAsync<string>(SettingsKeys.RuntimePreference, cancellationToken);
        var runtimePreference = Enum.TryParse<RuntimePreference>(savedRuntime, ignoreCase: true, out var parsedRuntime)
            ? parsedRuntime
            : RuntimePreference.Auto;

        await EnsureRuntimeAvailableAsync(runtimePreference, cancellationToken);

        _logger.LogInformation("Initializing Whisper with model type: {ModelType}", modelType);
        var modelPath = await _downloadService.EnsureModelAsync(modelType, cancellationToken);

        await WhisperRuntimeBootstrap.EnsureInitializedAsync(_settings, _logger);

        try
        {
            _factory = WhisperFactory.FromPath(modelPath);
        }
        catch (Exception ex) when (runtimePreference is RuntimePreference.Cuda or RuntimePreference.Vulkan)
        {
            throw new RuntimeUnavailableException(
                runtimePreference,
                $"Whisper.net failed to load the '{runtimePreference}' runtime. The native libraries may be missing or incompatible.",
                ex);
        }

        _logger.LogInformation("Whisper runtime loaded: {Runtime}", WhisperRuntimeBootstrap.LoadedRuntime?.ToString() ?? "unknown");
        AssertLoadedRuntimeMatches(runtimePreference);

        _processor = _factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        IsReady = true;
        _logger.LogInformation("Whisper model loaded successfully");
    }

    public async Task InitializeAsync(WhisperOptions options, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsReady && options == _currentOptions)
            return;

        // Options changed — unload the current model before reinitializing
        if (IsReady)
        {
            _logger.LogInformation("Whisper options changed, reinitializing");
            await UnloadAsync();
        }

        _logger.LogInformation(
            "Initializing Whisper with model: {Model}, language: {Language}, beamSize: {BeamSize}, temperature: {Temperature}",
            options.Model, options.Language, options.BeamSize, options.Temperature);

        await EnsureRuntimeAvailableAsync(options.RuntimePreference, cancellationToken);

        var modelPath = await _downloadService.EnsureModelAsync(options.Model, cancellationToken);

        WhisperRuntimeBootstrap.Initialize(options.RuntimePreference, _logger);

        try
        {
            _factory = WhisperFactory.FromPath(modelPath);
        }
        catch (Exception ex) when (options.RuntimePreference is RuntimePreference.Cuda or RuntimePreference.Vulkan)
        {
            throw new RuntimeUnavailableException(
                options.RuntimePreference,
                $"Whisper.net failed to load the '{options.RuntimePreference}' runtime. The native libraries may be missing or incompatible.",
                ex);
        }
        _logger.LogInformation("Whisper runtime loaded: {Runtime}", WhisperRuntimeBootstrap.LoadedRuntime?.ToString() ?? "unknown");
        AssertLoadedRuntimeMatches(options.RuntimePreference);

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

        if (options.TranslateToEnglish)
            builder.WithTranslate();

        if (!string.IsNullOrEmpty(options.InitialPrompt))
            builder.WithPrompt(options.InitialPrompt);

        _processor = builder.Build();
        _currentOptions = options;
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
