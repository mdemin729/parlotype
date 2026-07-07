using Microsoft.Extensions.Logging;
using Parlotype.Core.Settings;
using Parlotype.Core.Speech;
using SherpaOnnx;

namespace Parlotype.Platform.Speech;

/// <summary>
/// Speech recognition using NVIDIA Parakeet TDT v3 via sherpa-onnx, in-process.
/// CPU-only (the pre-built sherpa-onnx packages ship no GPU provider); the INT8
/// model decodes many times faster than real time on modern CPUs. The model
/// always auto-detects the spoken language — there is no language-forcing
/// parameter — so the selected source language is ignored.
/// </summary>
public sealed class ParakeetSpeechRecognizer : ISpeechRecognizer
{
    private readonly ISettingsService _settings;
    private readonly ILogger<ParakeetSpeechRecognizer> _logger;
    private readonly SemaphoreSlim _initLock = new(1, 1);
    private OfflineRecognizer? _recognizer;
    private bool _disposed;

    /// <summary>Audio sample rate the pipeline delivers (sherpa resamples if it differs).</summary>
    private const int SampleRate = 16000;

    public bool IsReady { get; private set; }

    public ParakeetSpeechRecognizer(
        ISettingsService settings,
        ILogger<ParakeetSpeechRecognizer> logger)
    {
        _settings = settings;
        _logger = logger;
    }

    /// <summary>Resolves the configured model, falling back to the catalog default.</summary>
    internal async Task<ParakeetModelInfo> ResolveModelAsync(CancellationToken cancellationToken)
    {
        var savedModel = await _settings.GetAsync<string>(SettingsKeys.SelectedParakeetModel, cancellationToken);
        return ParakeetModelInfo.GetById(savedModel) ?? ParakeetModelInfo.Default;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsReady)
            return;

        await _initLock.WaitAsync(cancellationToken);
        try
        {
            if (IsReady)
                return;

            var model = await ResolveModelAsync(cancellationToken);
            var modelDir = model.GetModelDirectory();

            var missing = model.FileNames
                .Where(f => !File.Exists(Path.Combine(modelDir, f)))
                .ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Parakeet model '{model.DisplayName}' is not downloaded " +
                    $"(missing: {string.Join(", ", missing)}). " +
                    "Download it first in Settings → Parakeet Model.");
            }

            _logger.LogInformation("Initializing Parakeet model: {Model}", model.ModelId);

            var config = new OfflineRecognizerConfig();
            config.ModelConfig.Transducer.Encoder = Path.Combine(modelDir, model.EncoderFileName);
            config.ModelConfig.Transducer.Decoder = Path.Combine(modelDir, model.DecoderFileName);
            config.ModelConfig.Transducer.Joiner = Path.Combine(modelDir, model.JoinerFileName);
            config.ModelConfig.Tokens = Path.Combine(modelDir, model.TokensFileName);
            config.ModelConfig.ModelType = "nemo_transducer";
            config.ModelConfig.NumThreads = Math.Max(1, Environment.ProcessorCount / 2);
            config.ModelConfig.Provider = "cpu";
            config.DecodingMethod = "greedy_search";

            // Construction loads the ~650 MB encoder synchronously (a few
            // seconds) — run off the calling thread so the UI stays responsive
            // and the loading spinner animates (ADR-038).
            _recognizer = await Task.Run(() => new OfflineRecognizer(config), cancellationToken)
                .ConfigureAwait(false);

            IsReady = true;
            _logger.LogInformation("Parakeet model loaded successfully");
        }
        finally
        {
            _initLock.Release();
        }
    }

    public async Task<TranscriptionResult> TranscribeAsync(
        ReadOnlyMemory<float> samples, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        var recognizer = _recognizer;
        if (!IsReady || recognizer is null)
            throw new InvalidOperationException("Speech recognizer is not initialized. Call InitializeAsync first.");

        cancellationToken.ThrowIfCancellationRequested();

        // Decode is synchronous and CPU-bound; sherpa-onnx cannot cancel
        // mid-decode, but VAD segments are short (seconds) so the tail cost of
        // a cancelled decode is negligible.
        var text = await Task.Run(() =>
        {
            using var stream = recognizer.CreateStream();
            stream.AcceptWaveform(SampleRate, samples.ToArray());
            recognizer.Decode(stream);
            return stream.Result.Text;
        }, cancellationToken).ConfigureAwait(false);

        _logger.LogDebug("Parakeet transcription completed: {Length} chars", text.Length);

        // The sherpa-onnx C# result exposes no confidence or detected-language
        // fields for NeMo transducer models.
        return new TranscriptionResult
        {
            Text = text.Trim(),
        };
    }

    public async Task UnloadAsync()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        await _initLock.WaitAsync();
        try
        {
            if (!IsReady)
                return;

            _recognizer?.Dispose();
            _recognizer = null;
            IsReady = false;
            _logger.LogInformation("Parakeet model unloaded");
        }
        finally
        {
            _initLock.Release();
        }
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
