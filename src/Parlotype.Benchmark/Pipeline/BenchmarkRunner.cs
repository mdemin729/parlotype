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
        string? tagsFilter = null,
        string? samplesFilter = null,
        CancellationToken cancellationToken = default)
    {
        var timestamp = DateTimeOffset.UtcNow;
        var runId = $"{timestamp:yyyyMMdd-HHmmss}-{config.Name}";

        // Initialize recognizer with config options
        var modelLoadSw = Stopwatch.StartNew();

        if (config.IsLlamaCpp)
        {
            progress?.Report("Starting llama-server (Gemma 4)...");
            await _recognizer.InitializeAsync(cancellationToken);
        }
        else if (config.IsParakeet)
        {
            // Model selection comes from settings (Program.cs injects an
            // in-memory settings service seeded from the benchmark config).
            progress?.Report("Loading Parakeet model...");
            await _recognizer.InitializeAsync(cancellationToken);
        }
        else
        {
            var whisper = config.EffectiveWhisper;
            var whisperOptions = new WhisperOptions
            {
                Model = whisper.Model,
                Language = whisper.Language,
                BeamSize = whisper.BeamSize,
                Temperature = whisper.Temperature,
                InitialPrompt = whisper.InitialPrompt,
                Threads = whisper.Threads,
                RuntimePreference = whisper.RuntimePreference,
            };

            progress?.Report("Loading Whisper model...");
            await _recognizer.InitializeAsync(whisperOptions, cancellationToken);
        }

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

        // Apply tag filter (AND logic: sample must have ALL specified tags)
        if (!string.IsNullOrEmpty(tagsFilter))
        {
            var requiredTags = tagsFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            allSamples = allSamples
                .Where(s => requiredTags.All(tag => s.Info.Tags.Contains(tag, StringComparer.OrdinalIgnoreCase)))
                .ToList();
            _logger.LogInformation("Tag filter '{Tags}' applied: {Count} samples remaining", tagsFilter, allSamples.Count);
        }

        // Apply sample ID filter
        if (!string.IsNullOrEmpty(samplesFilter))
        {
            var sampleIds = samplesFilter.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            var idSet = new HashSet<string>(sampleIds, StringComparer.OrdinalIgnoreCase);
            allSamples = allSamples
                .Where(s => idSet.Contains(s.Info.Id))
                .ToList();
            _logger.LogInformation("Sample filter '{Samples}' applied: {Count} samples remaining", samplesFilter, allSamples.Count);
        }

        if (allSamples.Count == 0)
            throw new InvalidOperationException("No samples remaining after applying filters.");

        _logger.LogInformation("Found {SampleCount} samples across {DatasetCount} dataset(s)",
            allSamples.Count, config.Datasets.Length);

        // Warm-up pass: one throwaway transcription of the first sample to prime CUDA / OS
        // file cache / first-inference paths so the timed loop reports steady-state numbers.
        // The same sample is re-run inside the timed loop, so all reported per-sample
        // numbers are warm. Exceptions propagate — a failed warm-up means the timed loop
        // would still be cold, so the run is invalid.
        double? warmupTimeMs = null;
        if (allSamples.Count > 0)
        {
            var (warmupInfo, warmupAudioPath) = allSamples[0];
            cancellationToken.ThrowIfCancellationRequested();
            progress?.Report($"Warming up on {warmupInfo.Id}...");
            _logger.LogInformation("Warm-up pass on sample {SampleId}", warmupInfo.Id);

            var warmupSw = Stopwatch.StartNew();
            var (warmupSamples, _) = AudioFileLoader.Load(warmupAudioPath);
            _ = await _recognizer.TranscribeAsync(warmupSamples.AsMemory(), cancellationToken);
            warmupSw.Stop();
            warmupTimeMs = warmupSw.Elapsed.TotalMilliseconds;
            _logger.LogInformation("Warm-up completed in {WarmupTimeMs:F0} ms", warmupTimeMs);
        }

        // Process each sample
        var sampleResults = new List<SampleResult>();
        double peakRamMb = 0;

        // GC baselines captured AFTER warm-up so per-sample GC counters reflect the
        // timed loop only (otherwise warm-up GC collections would pollute the summary).
        var gcGen0Before = GC.CollectionCount(0);
        var gcGen1Before = GC.CollectionCount(1);
        var gcGen2Before = GC.CollectionCount(2);

        for (int i = 0; i < allSamples.Count; i++)
        {
            var (sampleInfo, audioPath) = allSamples[i];
            cancellationToken.ThrowIfCancellationRequested();

            progress?.Report($"Processing sample {i + 1}/{allSamples.Count}: {sampleInfo.Id}");
            _logger.LogInformation("Processing sample {Index}/{Total}: {SampleId}", i + 1, allSamples.Count, sampleInfo.Id);

            var ramBefore = Process.GetCurrentProcess().WorkingSet64;
            var gcBefore = GC.GetAllocatedBytesForCurrentThread();

            var result = await ProcessSampleAsync(sampleInfo, audioPath, config.Vad, config.Repetitions, cancellationToken);

            result.RamDeltaMb = (Process.GetCurrentProcess().WorkingSet64 - ramBefore) / (1024.0 * 1024.0);
            result.GcAllocatedBytes = GC.GetAllocatedBytesForCurrentThread() - gcBefore;

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
            WarmupTimeMs = warmupTimeMs,
            PeakRamMb = peakRamMb,
            AvgRamDeltaMb = sampleResults.Count > 0 ? sampleResults.Average(s => s.RamDeltaMb) : 0,
            TotalGcAllocatedBytes = sampleResults.Sum(s => s.GcAllocatedBytes),
            GcGen0Collections = GC.CollectionCount(0) - gcGen0Before,
            GcGen1Collections = GC.CollectionCount(1) - gcGen1Before,
            GcGen2Collections = GC.CollectionCount(2) - gcGen2Before,
            Repetitions = config.Repetitions,
            WerStdDev = sampleResults.Count > 0 ? sampleResults.Average(s => s.WerStdDev) : 0,
            CerStdDev = sampleResults.Count > 0 ? sampleResults.Average(s => s.CerStdDev) : 0,
            WerCoeffOfVariation = sampleResults.Count > 0 && sampleResults.Average(s => s.Wer) > 0
                ? sampleResults.Average(s => s.WerStdDev) / sampleResults.Average(s => s.Wer) * 100 : 0,
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
        SampleInfo sampleInfo, string audioPath, VadConfig vadConfig, int repetitions, CancellationToken cancellationToken)
    {
        // Load and resample audio (once)
        var (samples, durationSeconds) = AudioFileLoader.Load(audioPath);

        // Optional VAD preprocessing (once)
        float[] audioForRecognition;
        List<float[]>? pipelineFlushSegments = null;

        if (vadConfig.Enabled && _vadService is not null)
        {
            var vadOptions = new VadOptions
            {
                Threshold = vadConfig.Threshold,
                SpeechPadMs = vadConfig.SpeechPadMs,
                MinSilenceDurationMs = vadConfig.MinSilenceDurationMs,
                MinSpeechDurationMs = vadConfig.MinSpeechDurationMs,
                InterSegmentSilenceMs = vadConfig.InterSegmentSilenceMs,
            };

            if (vadConfig.SilenceThresholdMs is { } silenceThresholdMs)
            {
                // Pipeline simulation mode: simulate real-time flush behavior
                pipelineFlushSegments = PipelineSimulator.Simulate(
                    samples.AsSpan(), _vadService, vadOptions, silenceThresholdMs);
                audioForRecognition = samples; // won't be used directly
                _logger.LogDebug("Pipeline simulation: {FlushCount} flush(es) from {Duration:F1}s audio with {ThresholdMs}ms threshold",
                    pipelineFlushSegments.Count, durationSeconds, silenceThresholdMs);
            }
            else
            {
                // One-shot mode (existing behavior)
                var speechSegments = _vadService.DetectSpeech(samples.AsSpan(), vadOptions);
                if (speechSegments.Count > 0)
                {
                    audioForRecognition = SpeechSegmentExtractor.Extract(
                        samples.AsSpan(), speechSegments, vadOptions.InterSegmentSilenceMs);
                    _logger.LogDebug("VAD: {SegmentCount} speech segments extracted from {Duration:F1}s audio",
                        speechSegments.Count, durationSeconds);
                }
                else
                {
                    audioForRecognition = samples;
                    _logger.LogDebug("VAD: no speech detected, using full audio");
                }
            }
        }
        else
        {
            audioForRecognition = samples;
        }

        var repetitionDetails = new List<RepetitionDetail>();
        string representativeHypothesis = "";

        for (int rep = 0; rep < repetitions; rep++)
        {
            var sw = Stopwatch.StartNew();
            TranscriptionResult transcription;

            if (pipelineFlushSegments is not null)
            {
                // Pipeline simulation mode: transcribe each flush segment and concatenate
                var texts = new List<string>();
                foreach (var segment in pipelineFlushSegments)
                {
                    var segResult = await _recognizer.TranscribeAsync(segment.AsMemory(), cancellationToken);
                    if (!string.IsNullOrWhiteSpace(segResult.Text))
                        texts.Add(segResult.Text.Trim());
                }
                transcription = new TranscriptionResult { Text = string.Join(" ", texts) };
            }
            else
            {
                transcription = await _recognizer.TranscribeAsync(audioForRecognition.AsMemory(), cancellationToken);
            }
            sw.Stop();

            var processingTimeMs = sw.Elapsed.TotalMilliseconds;
            var rtf = durationSeconds > 0 ? processingTimeMs / 1000.0 / durationSeconds : 0;
            var wer = EditDistanceCalculator.ComputeWer(sampleInfo.ReferenceText, transcription.Text);
            var cer = EditDistanceCalculator.ComputeCer(sampleInfo.ReferenceText, transcription.Text);

            if (rep == 0)
                representativeHypothesis = transcription.Text;

            repetitionDetails.Add(new RepetitionDetail
            {
                Repetition = rep + 1,
                ProcessingTimeMs = processingTimeMs,
                Rtf = rtf,
                Wer = wer,
                Cer = cer,
                HypothesisText = transcription.Text,
            });

            if (repetitions > 1)
                _logger.LogDebug("Sample {Id} rep {Rep}/{Total}: WER={Wer:F1}%, Time={Time:F0}ms",
                    sampleInfo.Id, rep + 1, repetitions, wer, processingTimeMs);
        }

        // Compute aggregates
        var meanWer = repetitionDetails.Average(r => r.Wer);
        var meanCer = repetitionDetails.Average(r => r.Cer);
        var meanRtf = repetitionDetails.Average(r => r.Rtf);
        var meanProcessingTime = repetitionDetails.Average(r => r.ProcessingTimeMs);

        var werStdDev = repetitions > 1 ? ComputeStdDev(repetitionDetails.Select(r => r.Wer)) : 0.0;
        var cerStdDev = repetitions > 1 ? ComputeStdDev(repetitionDetails.Select(r => r.Cer)) : 0.0;

        _logger.LogInformation(
            "Sample {Id}: WER={Wer:F1}%{StdDev}, CER={Cer:F1}%, RTF={Rtf:F3}, Time={Time:F0}ms",
            sampleInfo.Id, meanWer,
            repetitions > 1 ? $" (σ={werStdDev:F2})" : "",
            meanCer, meanRtf, meanProcessingTime);

        return new SampleResult
        {
            Id = sampleInfo.Id,
            ReferenceText = sampleInfo.ReferenceText,
            HypothesisText = representativeHypothesis,
            Wer = meanWer,
            Cer = meanCer,
            ProcessingTimeMs = meanProcessingTime,
            Rtf = meanRtf,
            WerStdDev = werStdDev,
            CerStdDev = cerStdDev,
            Repetitions = repetitions > 1 ? repetitionDetails : null,
        };
    }

    private static double ComputeStdDev(IEnumerable<double> values)
    {
        var list = values.ToList();
        if (list.Count <= 1) return 0.0;
        var mean = list.Average();
        var sumSquares = list.Sum(v => (v - mean) * (v - mean));
        return Math.Sqrt(sumSquares / (list.Count - 1));
    }

}
