using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Parlotype.Benchmark.Configuration;
using Parlotype.Benchmark.Metrics;
using Parlotype.Benchmark.Results;
using Parlotype.Core.Audio;
using Parlotype.Core.Speech;

namespace Parlotype.Benchmark.Pipeline;

/// <summary>Orchestrates a benchmark run: loads model, iterates samples, collects metrics.</summary>
public sealed class BenchmarkRunner
{
    private readonly ISpeechRecognizer _recognizer;
    private readonly IVadService? _vadService;
    private readonly ILogger<BenchmarkRunner> _logger;

    public BenchmarkRunner(ISpeechRecognizer recognizer, IVadService? vadService, ILogger<BenchmarkRunner> logger)
    {
        _recognizer = recognizer;
        _vadService = vadService;
        _logger = logger;
    }

    /// <summary>Runs the benchmark and returns the complete result.</summary>
    public async Task<BenchmarkResult> RunAsync(
        BenchmarkConfig config,
        string datasetsDir,
        IProgress<string>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var runId = $"{timestamp:yyyyMMdd-HHmmss}-{config.Name}";

        // Initialize recognizer with config options
        var whisperOptions = new WhisperOptions
        {
            Model = config.Whisper.Model,
            Language = config.Whisper.Language,
            BeamSize = config.Whisper.BeamSize,
            Temperature = config.Whisper.Temperature,
            InitialPrompt = config.Whisper.InitialPrompt,
        };

        progress?.Report("Loading Whisper model...");
        var modelLoadSw = Stopwatch.StartNew();
        await _recognizer.InitializeAsync(whisperOptions, cancellationToken);
        modelLoadSw.Stop();
        var modelLoadTimeMs = modelLoadSw.Elapsed.TotalMilliseconds;
        _logger.LogInformation("Model loaded in {ModelLoadTimeMs:F0} ms", modelLoadTimeMs);

        // Load all dataset manifests and collect samples
        var allSamples = new List<(SampleInfo Info, string AudioPath)>();
        foreach (var datasetName in config.Datasets)
        {
            var manifestPath = Path.Combine(datasetsDir, datasetName, "manifest.json");
            if (!File.Exists(manifestPath))
            {
                _logger.LogWarning("Manifest not found: {Path}", manifestPath);
                continue;
            }

            var manifestJson = await File.ReadAllTextAsync(manifestPath, cancellationToken);
            var manifest = System.Text.Json.JsonSerializer.Deserialize<DatasetManifest>(manifestJson,
                new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
                ?? throw new InvalidOperationException($"Failed to deserialize manifest: {manifestPath}");

            foreach (var sample in manifest.Samples)
            {
                var audioPath = Path.Combine(datasetsDir, datasetName, sample.File);
                allSamples.Add((sample, audioPath));
            }
        }

        if (allSamples.Count == 0)
            throw new InvalidOperationException("No samples found in the specified datasets.");

        _logger.LogInformation("Found {SampleCount} samples across {DatasetCount} dataset(s)",
            allSamples.Count, config.Datasets.Length);

        // Process each sample
        var sampleResults = new List<SampleResult>();
        double peakRamMb = 0;

        for (int i = 0; i < allSamples.Count; i++)
        {
            var (sampleInfo, audioPath) = allSamples[i];
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report($"Processing sample {i + 1}/{allSamples.Count}: {sampleInfo.Id}");
            _logger.LogInformation("Processing sample {Index}/{Total}: {SampleId}", i + 1, allSamples.Count, sampleInfo.Id);

            var result = await ProcessSampleAsync(sampleInfo, audioPath, config.Vad.Enabled, cancellationToken);
            sampleResults.Add(result);

            // Track peak RAM (process-wide approximation)
            var currentPeakRam = Process.GetCurrentProcess().PeakWorkingSet64 / (1024.0 * 1024.0);
            if (currentPeakRam > peakRamMb)
                peakRamMb = currentPeakRam;
        }

        // Compute summary
        var summary = new BenchmarkSummary
        {
            TotalSamples = sampleResults.Count,
            AverageWer = sampleResults.Count > 0 ? sampleResults.Average(s => s.Wer) : 0,
            AverageCer = sampleResults.Count > 0 ? sampleResults.Average(s => s.Cer) : 0,
            AverageRtf = sampleResults.Count > 0 ? sampleResults.Average(s => s.Rtf) : 0,
            TotalProcessingTimeMs = sampleResults.Sum(s => s.ProcessingTimeMs),
            ModelLoadTimeMs = modelLoadTimeMs,
            PeakRamMb = peakRamMb,
        };

        return new BenchmarkResult
        {
            RunId = runId,
            Timestamp = timestamp,
            Configuration = config,
            Environment = EnvironmentInfo.Capture(),
            Summary = summary,
            Samples = sampleResults,
        };
    }

    private async Task<SampleResult> ProcessSampleAsync(
        SampleInfo sampleInfo, string audioPath, bool vadEnabled, CancellationToken cancellationToken)
    {
        // Load and resample audio
        var (samples, durationSeconds) = AudioFileLoader.Load(audioPath);

        // Optional VAD preprocessing
        float[] audioForRecognition;
        if (vadEnabled && _vadService is not null)
        {
            var speechSegments = _vadService.DetectSpeech(samples.AsSpan());
            if (speechSegments.Count > 0)
            {
                audioForRecognition = ExtractSpeechSegments(samples, speechSegments);
                _logger.LogDebug("VAD: {SegmentCount} speech segments extracted from {Duration:F1}s audio",
                    speechSegments.Count, durationSeconds);
            }
            else
            {
                // No speech detected — feed entire audio
                audioForRecognition = samples;
                _logger.LogDebug("VAD: no speech detected, using full audio");
            }
        }
        else
        {
            audioForRecognition = samples;
        }

        // Transcribe with timing
        var sw = Stopwatch.StartNew();
        var transcription = await _recognizer.TranscribeAsync(audioForRecognition.AsMemory(), cancellationToken);
        sw.Stop();

        var processingTimeMs = sw.Elapsed.TotalMilliseconds;
        var rtf = durationSeconds > 0 ? processingTimeMs / 1000.0 / durationSeconds : 0;

        // Compute metrics
        var wer = EditDistanceCalculator.ComputeWer(sampleInfo.ReferenceText, transcription.Text);
        var cer = EditDistanceCalculator.ComputeCer(sampleInfo.ReferenceText, transcription.Text);

        _logger.LogInformation(
            "Sample {Id}: WER={Wer:F1}%, CER={Cer:F1}%, RTF={Rtf:F3}, Time={Time:F0}ms",
            sampleInfo.Id, wer, cer, rtf, processingTimeMs);

        return new SampleResult
        {
            Id = sampleInfo.Id,
            ReferenceText = sampleInfo.ReferenceText,
            HypothesisText = transcription.Text,
            Wer = wer,
            Cer = cer,
            ProcessingTimeMs = processingTimeMs,
            Rtf = rtf,
        };
    }

    private static float[] ExtractSpeechSegments(float[] samples, List<VadSpeechSegment> segments)
    {
        var totalLength = segments.Sum(s => Math.Min(s.EndSample, samples.Length) - s.StartSample);
        var result = new float[totalLength];
        var offset = 0;

        foreach (var segment in segments)
        {
            var start = segment.StartSample;
            var end = Math.Min(segment.EndSample, samples.Length);
            var length = end - start;
            Array.Copy(samples, start, result, offset, length);
            offset += length;
        }

        return result;
    }
}
