# Phase 1 Implementation Plan — Parlotype Benchmark MVP

## Problem Statement

Parlotype needs an objective, reproducible benchmark tool to evaluate speech recognition quality. Phase 1 delivers the minimum viable benchmark: a console app that runs a Whisper model against a WAV dataset, computes WER/CER/RTF metrics, and outputs JSON results with a console summary table.

## Scope

**In scope (Phase 1):**
- New `Parlotype.Benchmark` console project
- Modify `WhisperSpeechRecognizer` to accept optional parameters (language, beam size, temperature)
- Dataset manifest loading (JSON) with WAV audio files
- Audio file loading → 16kHz mono float (reuse NAudio resampling pattern)
- VAD toggle (on/off, reuse `SileroVadService` with current defaults)
- WER and CER calculation (custom Levenshtein edit distance)
- RTF and processing time measurement
- Model load time measurement
- Peak RAM measurement (`Process.GetCurrentProcess().PeakWorkingSet64`)
- Text normalization before WER/CER comparison
- JSON result output (one file per run)
- Console summary table (Spectre.Console)
- JSON run configuration file
- 3–5 bundled WAV test samples with ground truth
- Unit tests for metrics calculation and text normalization

**Out of scope (deferred to Phase 2+):**
- SQLite index, `compare`/`list` commands
- CSV/Markdown export
- VRAM measurement, CPU/GPU utilization
- Beam size/temperature parameter sweeps
- Result stability across repeated runs
- CI/CD integration

## Key Design Decisions

1. **Modify `WhisperSpeechRecognizer`** — add optional `language`, `beamSize`, `temperature` parameters to `InitializeAsync` (backward-compatible; desktop app continues using defaults).
2. **VAD toggle** — when enabled, run `SileroVadService.DetectSpeech()` on the audio, extract speech segments, concatenate, and feed to Whisper. When disabled, feed entire audio.
3. **Spectre.Console** for rich table output and progress bars.
4. **System.CommandLine** for CLI argument parsing.
5. **Custom Levenshtein** for WER/CER — no external NuGet dependency; straightforward DP algorithm.
6. **Reuse `TestAudioHelper` pattern** for WAV loading and resampling (NAudio `WaveFileReader` → `ToMono()` → `WdlResamplingSampleProvider`).

---

## Project Structure

```
src/
  Parlotype.Benchmark/
    Parlotype.Benchmark.csproj        # Console app, refs Platform + Core
    Program.cs                        # CLI entry point (System.CommandLine)
    Configuration/
      BenchmarkConfig.cs              # Run configuration model (deserialized from JSON)
      DatasetManifest.cs              # Dataset manifest model
    Metrics/
      TextNormalizer.cs               # Normalize text before WER/CER comparison
      EditDistanceCalculator.cs       # Levenshtein DP for word-level and char-level
      MetricsResult.cs                # Per-sample metrics (WER, CER, RTF, time)
    Pipeline/
      AudioFileLoader.cs              # WAV → 16kHz mono float[] (NAudio)
      BenchmarkRunner.cs              # Orchestrates: load model → iterate samples → collect metrics
    Results/
      BenchmarkResult.cs              # Full run result model
      SampleResult.cs                 # Per-sample result
      EnvironmentInfo.cs              # OS, CPU, RAM, .NET version, package versions
      JsonResultStore.cs              # Serialize/save result JSON files
    Reporting/
      ConsoleReporter.cs              # Spectre.Console summary table + progress

  Parlotype.Benchmark.Tests/
    Parlotype.Benchmark.Tests.csproj  # Unit tests for metrics + text normalization
    EditDistanceCalculatorTests.cs
    TextNormalizerTests.cs

datasets/
  smoke-test/
    manifest.json                     # 3–5 samples with ground truth
    samples/
      sample-001.wav                  # Short English speech clips
      sample-002.wav
      sample-003.wav
```

---

## Todos

### 1. `create-benchmark-project` — Create Parlotype.Benchmark console project

Create `src/Parlotype.Benchmark/Parlotype.Benchmark.csproj` as a console app targeting `net10.0`. Add project references to `Parlotype.Core` and `Parlotype.Platform`. Add NuGet packages: `System.CommandLine`, `Spectre.Console`. Add project to `Parlotype.slnx`. Verify `dotnet build` succeeds with zero warnings.

### 2. `create-data-models` — Define configuration and result data models

Create the following model classes using `System.Text.Json` serialization:

**`BenchmarkConfig`** — deserialized from the run configuration JSON:
- `Name`, `Description` (string)
- `Datasets` (string[] — dataset directory names)
- `Repetitions` (int, default 1)
- `Whisper` → `Model` (WhisperModelType), `Language` ("auto" or code), `BeamSize` (int), `Temperature` (float)
- `Vad` → `Enabled` (bool)
- (Runtime/preprocessing deferred to Phase 3)

**`DatasetManifest`** — deserialized from `manifest.json`:
- `Name`, `Description`, `Language` (string)
- `Samples[]` → `Id`, `File` (relative path), `ReferenceText`, `Language`, `DurationSeconds`, `Tags`

**`BenchmarkResult`** — the full output model:
- `RunId`, `Timestamp`, `Configuration` (BenchmarkConfig snapshot), `Environment` (EnvironmentInfo)
- `Summary` → `TotalSamples`, `AverageWer`, `AverageCer`, `AverageRtf`, `TotalProcessingTimeMs`, `ModelLoadTimeMs`, `PeakRamMb`
- `Samples[]` → `SampleResult` (Id, ReferenceText, HypothesisText, Wer, Cer, ProcessingTimeMs, Rtf)

**`EnvironmentInfo`** — captured at runtime:
- `Os`, `Cpu`, `RamGb`, `DotnetVersion`, `WhisperNetVersion`

### 3. `modify-whisper-recognizer` — Add optional parameters to WhisperSpeechRecognizer

Modify `WhisperSpeechRecognizer.InitializeAsync()` to accept optional parameters. Add a new overload or optional parameter object:

```csharp
public record WhisperOptions(
    WhisperModelType Model = WhisperModelType.Base,
    string Language = "auto",
    int BeamSize = 1,
    float Temperature = 0.0f);

Task InitializeAsync(WhisperOptions? options = null, CancellationToken ct = default);
```

When `options` is null, behavior is identical to current (reads from settings). When provided, use the explicit values. Apply `BeamSize` and `Temperature` in the `WhisperProcessorBuilder` chain. Update `ISpeechRecognizer` interface to add the overload. Ensure existing desktop app callers are unaffected (they pass no options).

### 4. `implement-text-normalizer` — Text normalization for WER/CER comparison

Create `TextNormalizer.cs` with a static `Normalize(string text)` method:
1. Lowercase
2. Remove punctuation (all non-letter, non-digit, non-space chars)
3. Collapse multiple whitespace to single space
4. Trim

This is critical for fair WER/CER comparison — Whisper output may differ in casing/punctuation from ground truth.

### 5. `implement-edit-distance` — WER and CER calculation

Create `EditDistanceCalculator.cs` with:

```csharp
public static class EditDistanceCalculator
{
    // Returns (substitutions, deletions, insertions, referenceLength)
    public static (int S, int D, int I, int N) Compute(string[] reference, string[] hypothesis);

    // WER = (S + D + I) / N * 100
    public static double ComputeWer(string referenceText, string hypothesisText);

    // CER = character-level edit distance / reference char count * 100
    public static double ComputeCer(string referenceText, string hypothesisText);
}
```

Use standard Wagner-Fischer DP algorithm. Tokenize at word level for WER, character level for CER. Apply `TextNormalizer.Normalize()` to both texts before computing.

### 6. `implement-audio-loader` — WAV file loading and resampling

Create `AudioFileLoader.cs` that loads a WAV file and returns 16kHz mono float samples:

```csharp
public static class AudioFileLoader
{
    public static (float[] Samples, double DurationSeconds) LoadWav(string filePath);
}
```

Reuse the `TestAudioHelper` pattern: `WaveFileReader` → `ToMono()` → `WdlResamplingSampleProvider(16000)` → read all samples. Return samples + computed duration (samples.Length / 16000.0).

### 7. `implement-benchmark-runner` — Core benchmark orchestration

Create `BenchmarkRunner` class that:
1. Reads `BenchmarkConfig` from JSON file
2. Reads `DatasetManifest` from each dataset directory
3. Initializes `WhisperSpeechRecognizer` with config options (measure model load time)
4. Optionally initializes `SileroVadService`
5. Iterates each sample:
   a. Load WAV via `AudioFileLoader`
   b. If VAD enabled: run `DetectSpeech()`, extract speech segments, concatenate
   c. Start timer
   d. Call `TranscribeAsync()` on the recognizer
   e. Stop timer
   f. Compute WER, CER using `EditDistanceCalculator`
   g. Compute RTF = processingTime / audioDuration
   h. Record peak RAM (`Process.GetCurrentProcess().PeakWorkingSet64`)
   i. Store `SampleResult`
6. Compute summary aggregates (averages)
7. Return `BenchmarkResult`

Progress reporting via `IProgress<string>` or Spectre.Console `AnsiConsole.Status()`.

DI: inject `ISpeechRecognizer`, `IVadService`, `IModelDownloadService`, `ILogger`.

### 8. `implement-result-store` — JSON result serialization

Create `JsonResultStore.cs`:
- `SaveAsync(BenchmarkResult result, string outputDir)` — serializes to `{timestamp}-{config-name}.json`
- `LoadAsync(string filePath)` — deserializes a result file
- Uses `System.Text.Json` with `JsonSerializerOptions { WriteIndented = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase }`

### 9. `implement-console-reporter` — Spectre.Console summary output

Create `ConsoleReporter.cs` using Spectre.Console:
- Display a progress bar while benchmark runs
- After completion, display a summary table:

```
┌──────────────┬───────────────────────┐
│ Metric       │ Value                 │
├──────────────┼───────────────────────┤
│ Model        │ Base                  │
│ Samples      │ 5                     │
│ Avg WER      │ 8.5%                  │
│ Avg CER      │ 3.1%                  │
│ Avg RTF      │ 0.25                  │
│ Model Load   │ 450 ms                │
│ Total Time   │ 12.5 s                │
│ Peak RAM     │ 1200 MB               │
└──────────────┴───────────────────────┘
```

- Display a per-sample breakdown table (Id, WER, CER, RTF, Time)

### 10. `implement-cli` — System.CommandLine entry point

Create `Program.cs` with a single `run` command for Phase 1:

```bash
parlotype-bench run --config <path-to-config.json> --datasets <datasets-dir> --output <results-dir>
```

Options:
- `--config` (required) — path to run configuration JSON
- `--datasets` (required) — root directory containing dataset folders
- `--output` (optional, default `./results`) — where to save result JSON
- `--verbose` / `-v` — enable debug logging

Wire up DI container: register Platform services + benchmark services. Run `BenchmarkRunner`, save results via `JsonResultStore`, display via `ConsoleReporter`.

### 11. `create-sample-dataset` — Bundle 3–5 WAV samples with ground truth

Create `datasets/smoke-test/manifest.json` with 3–5 short English speech WAV files. Use the existing `kennedy.wav` test resource as one sample. Source 2-4 additional short public domain WAV clips (or generate via TTS for testing purposes). Each sample needs accurate `referenceText` ground truth.

### 12. `create-benchmark-tests` — Unit tests for metrics and normalization

Create `src/Parlotype.Benchmark.Tests/` project with xUnit tests:

**`TextNormalizerTests.cs`:**
- Lowercases text
- Removes punctuation
- Collapses whitespace
- Handles empty/null input

**`EditDistanceCalculatorTests.cs`:**
- Perfect match → WER=0%, CER=0%
- Single substitution → correct WER
- Insertions and deletions
- Empty reference / empty hypothesis edge cases
- Known WER/CER values from standard examples

**`BenchmarkConfigTests.cs`:**
- Deserializes valid JSON config
- Applies defaults for missing optional fields

### 13. `create-sample-config` — Example run configuration JSON

Create `datasets/smoke-test-config.json`:
```json
{
  "name": "smoke-test-baseline",
  "description": "Quick smoke test with Base model",
  "datasets": ["smoke-test"],
  "repetitions": 1,
  "whisper": {
    "model": "Base",
    "language": "auto",
    "beamSize": 1,
    "temperature": 0.0
  },
  "vad": {
    "enabled": false
  }
}
```

### 14. `verify-build-and-run` — Build, test, and smoke-run the benchmark

- `dotnet build Parlotype.slnx` — zero warnings
- `dotnet test` — all tests pass (existing + new)
- `dotnet run --project src/Parlotype.Benchmark -- run --config datasets/smoke-test-config.json --datasets datasets` — produces JSON result + console output

---

## Dependencies Between Todos

```
create-benchmark-project
  ├── create-data-models
  │     ├── implement-benchmark-runner
  │     ├── implement-result-store
  │     └── create-sample-config
  ├── implement-text-normalizer
  │     └── implement-edit-distance
  │           └── implement-benchmark-runner
  ├── implement-audio-loader
  │     └── implement-benchmark-runner
  └── implement-console-reporter

modify-whisper-recognizer
  └── implement-benchmark-runner

create-sample-dataset (independent)

implement-benchmark-runner
  ├── implement-cli
  └── implement-result-store

implement-cli
  └── verify-build-and-run

create-benchmark-tests
  depends on: implement-text-normalizer, implement-edit-distance, create-data-models

verify-build-and-run
  depends on: all above
```

## Notes

- The existing `ISpeechRecognizer` interface needs a new `InitializeAsync` overload. This is a **breaking interface change** — but only `WhisperSpeechRecognizer` implements it, and the desktop app uses the no-args version.
- Peak RAM measurement via `Process.PeakWorkingSet64` is process-wide, not per-inference. It's a rough approximation for Phase 1 — Phase 3 can use more precise per-allocation tracking.
- The benchmark app does not reference `Parlotype.Desktop` — it uses Platform + Core directly and sets up its own DI container.
- `System.CommandLine` is still in preview for .NET 10 — verify the latest stable version at implementation time.
