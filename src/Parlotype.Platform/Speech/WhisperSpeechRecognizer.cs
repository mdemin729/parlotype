using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using Whisper.net;

namespace Parlotype.Platform.Speech;

/// <summary>Speech recognition using Whisper.net with automatic model download.</summary>
public sealed class WhisperSpeechRecognizer : ISpeechRecognizer
{
    private readonly IModelDownloadService _downloadService;
    private readonly ISettingsService _settings;
    private readonly IVulkanEnvironmentProvider _vulkan;
    private readonly IWhisperRuntimeStatus _runtimeStatus;
    private readonly ILogger<WhisperSpeechRecognizer> _logger;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private WhisperOptions? _currentOptions;
    private bool _disposed;

    public bool IsReady { get; private set; }

    public WhisperSpeechRecognizer(
        IModelDownloadService downloadService,
        ISettingsService settings,
        IVulkanEnvironmentProvider vulkan,
        ILogger<WhisperSpeechRecognizer> logger,
        IWhisperRuntimeStatus? runtimeStatus = null)
    {
        _downloadService = downloadService;
        _settings = settings;
        _vulkan = vulkan;
        // Defaults to the real process-wide latch; tests inject a fake so they never
        // have to mutate Whisper.net's global state out from under parallel tests.
        _runtimeStatus = runtimeStatus ?? new WhisperRuntimeStatus();
        _logger = logger;
    }

    private async Task EnsureRuntimeAvailableAsync(RuntimePreference preference, CancellationToken cancellationToken)
    {
        if (preference is not RuntimePreference.Vulkan)
            return;

        var vulkan = await _vulkan.GetAsync(cancellationToken).ConfigureAwait(false);
        if (!vulkan.HasVulkanLoader)
            throw new RuntimeUnavailableException(
                RuntimePreference.Vulkan,
                "the Vulkan loader (vulkan-1.dll) was not found. Install your GPU vendor's latest drivers or the Vulkan SDK from https://vulkan.lunarg.com/sdk/home, then restart Parlotype.");
    }

    /// <summary>
    /// Fails fast when this process already loaded a different runtime. Whisper.net
    /// resolves its native library once per process, so a preference change made
    /// after the first model load can only be applied by restarting the app — and
    /// finding that out <i>after</i> loading several GB of weights is both slow and,
    /// historically, leaky. Cheap enough to run before the model download.
    /// </summary>
    private void AssertRuntimeStillSelectable(RuntimePreference preference)
    {
        if (!_runtimeStatus.RequiresRestartFor(preference))
            return;

        throw new RuntimeUnavailableException(
            preference,
            $"this session already loaded the '{_runtimeStatus.LoadedRuntimeName}' runtime. Whisper's runtime is chosen once per process, so restart Parlotype to switch to '{preference}'.",
            requiresRestart: true);
    }

    /// <summary>
    /// Post-load counterpart of <see cref="AssertRuntimeStillSelectable"/>: catches the
    /// case where the library order was latched by an earlier (failed) initialization,
    /// so nothing was loaded yet but the order in effect is not the one we asked for.
    /// </summary>
    private static void AssertLoadedRuntimeMatches(RuntimePreference preference)
    {
        var loaded = WhisperRuntimeBootstrap.LoadedRuntime;
        if (WhisperRuntimeBootstrap.IsSatisfiedBy(preference, loaded))
            return;

        throw new RuntimeUnavailableException(
            preference,
            $"Whisper.net loaded '{loaded?.ToString() ?? "(none)"}' instead of '{preference}'. The runtime order was fixed earlier in this session — restart Parlotype to switch.",
            requiresRestart: true);
    }

    /// <summary>
    /// Creates the factory and verifies the loaded runtime, disposing the factory if
    /// verification fails. <see cref="WhisperFactory"/> owns the native model context
    /// (multiple GB for large models) and has no finalizer, so a factory that escapes
    /// without a <c>Dispose</c> leaks that memory for the lifetime of the process.
    /// </summary>
    private WhisperFactory CreateVerifiedFactory(string modelPath, RuntimePreference preference)
    {
        WhisperFactory factory;
        try
        {
            factory = WhisperFactory.FromPath(modelPath);
        }
        catch (Exception ex) when (preference is RuntimePreference.Vulkan)
        {
            throw new RuntimeUnavailableException(
                preference,
                $"Whisper.net failed to load the '{preference}' runtime. The native libraries may be missing or incompatible.",
                ex);
        }

        _logger.LogInformation("Whisper runtime loaded: {Runtime}", WhisperRuntimeBootstrap.LoadedRuntime?.ToString() ?? "unknown");

        try
        {
            AssertLoadedRuntimeMatches(preference);
        }
        catch
        {
            factory.Dispose();
            throw;
        }

        return factory;
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
        AssertRuntimeStillSelectable(runtimePreference);

        _logger.LogInformation("Initializing Whisper with model type: {ModelType}", modelType);
        var modelPath = await _downloadService.EnsureModelAsync(modelType, cancellationToken);

        await WhisperRuntimeBootstrap.EnsureInitializedAsync(_settings, _logger);

        // Factory + processor build are synchronous and CPU-bound — run them off
        // the calling thread so the UI thread stays free and the spinner animates.
        await Task.Run(() =>
        {
            var factory = CreateVerifiedFactory(modelPath, runtimePreference);

            // Publish the factory only once the processor is built: a half-initialized
            // recognizer stays IsReady == false, and UnloadAsync would then skip
            // disposing whatever _factory held.
            try
            {
                _processor = factory.CreateBuilder()
                    .WithLanguage("auto")
                    .Build();
            }
            catch
            {
                factory.Dispose();
                throw;
            }

            _factory = factory;
        }, cancellationToken).ConfigureAwait(false);

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
        AssertRuntimeStillSelectable(options.RuntimePreference);

        var modelPath = await _downloadService.EnsureModelAsync(options.Model, cancellationToken);

        // Native runtime init + factory + processor build are synchronous and
        // CPU-bound. Run them off the calling thread so the UI thread stays free
        // and the loading spinner keeps animating. (The llama.cpp engine loads via
        // an out-of-process server, so it never blocked the UI in the first place.)
        await Task.Run(() =>
        {
            WhisperRuntimeBootstrap.Initialize(options.RuntimePreference, _logger);

            var factory = CreateVerifiedFactory(modelPath, options.RuntimePreference);

            // Publish the factory only once the processor is built — see the note in
            // the parameterless overload.
            try
            {
                var builder = factory.CreateBuilder()
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
            }
            catch
            {
                factory.Dispose();
                throw;
            }

            _factory = factory;
        }, cancellationToken).ConfigureAwait(false);

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

        // Deliberately not gated on IsReady: native resources are released whenever
        // they exist, so a partially initialized recognizer can never strand them.
        if (_processor is null && _factory is null)
        {
            IsReady = false;
            return;
        }

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
