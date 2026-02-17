using Parlotype.Core.Speech;
using Whisper.net;
using Whisper.net.Ggml;

namespace Parlotype.Platform.Speech;

/// <summary>Speech recognition using Whisper.net with automatic model download.</summary>
public sealed class WhisperSpeechRecognizer : ISpeechRecognizer
{
    private readonly GgmlType _modelType;
    private WhisperFactory? _factory;
    private WhisperProcessor? _processor;
    private bool _disposed;

    public bool IsReady { get; private set; }

    public WhisperSpeechRecognizer(GgmlType modelType = GgmlType.Base)
    {
        _modelType = modelType;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (IsReady)
            return;

        var modelPath = await EnsureModelAsync(_modelType, cancellationToken);

        _factory = WhisperFactory.FromPath(modelPath);
        _processor = _factory.CreateBuilder()
            .WithLanguage("auto")
            .Build();

        IsReady = true;
    }

    public async Task<TranscriptionResult> TranscribeAsync(ReadOnlyMemory<byte> pcmData, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);

        if (!IsReady || _processor is null)
            throw new InvalidOperationException("Speech recognizer is not initialized. Call InitializeAsync first.");

        // Convert 16-bit PCM bytes to float samples
        var samples = ConvertPcmToFloat(pcmData.Span);

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

        return new TranscriptionResult
        {
            Text = text,
            Confidence = avgConfidence,
            DetectedLanguage = language
        };
    }

    private static float[] ConvertPcmToFloat(ReadOnlySpan<byte> pcmBytes)
    {
        int sampleCount = pcmBytes.Length / 2;
        var samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            short sample = (short)(pcmBytes[i * 2] | (pcmBytes[i * 2 + 1] << 8));
            samples[i] = sample / (float)short.MaxValue;
        }

        return samples;
    }

    private static async Task<string> EnsureModelAsync(GgmlType modelType, CancellationToken cancellationToken)
    {
        var cacheDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "parlotype", "models");
        Directory.CreateDirectory(cacheDir);

        var modelFileName = $"ggml-{modelType.ToString().ToLowerInvariant()}.bin";
        var modelPath = Path.Combine(cacheDir, modelFileName);

        if (!File.Exists(modelPath))
        {
            using var modelStream = await WhisperGgmlDownloader.Default
                .GetGgmlModelAsync(modelType, cancellationToken: cancellationToken);
            using var fileStream = File.Create(modelPath);
            await modelStream.CopyToAsync(fileStream, cancellationToken);
        }

        return modelPath;
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;

        _disposed = true;

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
    }
}
