# Phase 3 Implementation Plan — Extended Metrics & Parameter Sweeps

## Problem Statement

Phases 1 and 2 delivered a working benchmark CLI with single-config runs, JSON/SQLite storage, comparison, and export. However, several capabilities from the requirements remain unimplemented:

- The `repetitions` field in `BenchmarkConfig` is declared but **completely ignored** by `BenchmarkRunner`.
- There is no way to run parameter sweeps (varying beam size, temperature, language, threads across a combinatorial grid).
- RAM tracking is process-wide (not per-sample), and there are no GC metrics.
- There is no CI/CD regression detection — no way to compare against a baseline and fail on regression.
- CPU thread count is not configurable (Whisper.net supports `WithThreads()` but it's not exposed).

Phase 3 closes these gaps.

## Scope

**In scope:**
- Repetition support — run each sample N times, compute mean/stddev for WER/CER/RTF
- Result stability metrics — standard deviation, min/max, coefficient of variation
- Parameter sweep config — JSON file with arrays per axis, generates all combinations
- `sweep` CLI command — runs the full parameter grid, produces one result per combination
- CPU thread count — add `Threads` to `WhisperOptions`, `WhisperConfig`, and `WhisperSpeechRecognizer`
- Per-sample memory metrics — RAM delta and GC allocations per sample
- `check` CLI command — compare latest run against a baseline, exit non-zero on regression
- GitHub Actions workflow for automated regression detection
- Unit tests for all new functionality

**Out of scope (Phase 4):**
- VRAM / GPU metrics (current codebase is CPU-only)
- Punctuation and capitalization metrics
- Streaming mode / first result latency
- GPU-specific runtimes (CUDA, Vulkan)
- Automated grid search optimization
- Grafana / dashboard integration

## Phase 2 Baseline (What Exists)

| Component | Location | Status |
|-----------|----------|--------|
| CLI: `run`, `import`, `list`, `compare`, `export` | `Program.cs` | ✅ |
| JSON + SQLite storage | `Results/JsonResultStore.cs`, `Results/SqliteResultIndex.cs` | ✅ |
| Comparison engine | `Reporting/ResultComparer.cs`, `ComparisonResult.cs` | ✅ |
| CSV/Markdown/JSON export | `Reporting/CsvFormatter.cs`, `MarkdownFormatter.cs` | ✅ |
| Console reporter with comparison | `Reporting/ConsoleReporter.cs` | ✅ |
| Tag/sample filtering | `BenchmarkRunner.RunAsync()`, `Program.cs` | ✅ |
| `BenchmarkConfig.Repetitions` field | `Configuration/BenchmarkConfig.cs` | ⚠️ Declared, not used |
| Thread count | Not exposed | ❌ |
| Per-sample RAM | Not tracked | ❌ |
| Parameter sweeps | Not implemented | ❌ |
| CI/CD check | Not implemented | ❌ |
| Unit tests | 63 total (22 benchmark + 10 desktop + 7 platform + 24 Phase 2) | ✅ |

---

## Design Decisions

### Sweep Configuration Format

A sweep config is a JSON file with arrays of values for each parameter axis. The benchmark generates the Cartesian product of all axes. Example:

```json
{
  "name": "model-comparison-sweep",
  "description": "Compare Base vs Small with different beam sizes",
  "datasets": ["librispeech-clean"],
  "repetitions": 3,
  "sweep": {
    "whisper.model": ["Base", "Small"],
    "whisper.beamSize": [1, 5],
    "whisper.temperature": [0.0],
    "whisper.language": ["auto"],
    "whisper.threads": [4],
    "vad.enabled": [true, false]
  }
}
```

This generates `2 × 2 × 1 × 1 × 1 × 2 = 8` configurations. Each combination gets a unique run ID derived from the sweep name and parameter values.

The `sweep` section uses dot-notation paths (e.g., `whisper.model`) mapping to arrays of values. The runner expands these into individual `BenchmarkConfig` instances.

### Repetition / Stability Model

When `repetitions > 1`:
1. Each sample is run N times (N = repetitions).
2. Per-sample results store all N timing/WER/CER values.
3. `SampleResult` gains `RepetitionResults` (list of per-repetition data) plus aggregated stats (mean, stddev, min, max).
4. Summary metrics use the **mean** across repetitions (not a single run).
5. A new `StabilityMetrics` section in `BenchmarkSummary` reports WER/CER standard deviation across repetitions.

### Check Command / CI Integration

```bash
parlotype-bench check --baseline <run-id> --current <run-id> \
  --output results --max-wer-delta 2.0 --max-cer-delta 1.0
```

- Compares current run against a baseline.
- Exits with code 0 if all deltas are within thresholds, code 1 if any regression exceeds thresholds.
- Outputs a concise summary suitable for CI logs.
- GitHub Actions workflow runs the benchmark on push/PR and compares against a committed baseline run ID.

### Memory Metrics

- **Per-sample RAM delta**: `Process.GetCurrentProcess().WorkingSet64` before and after each sample.
- **GC allocations**: `GC.GetAllocatedBytesForCurrentThread()` before and after each sample.
- **GC collections**: `GC.CollectionCount(gen)` per generation before/after run.
- These are stored in `SampleResult` and `BenchmarkSummary`.

---

## Todos

### 1. `add-threads-to-whisper` — Expose CPU thread count in WhisperOptions and WhisperSpeechRecognizer

Add `Threads` property to `WhisperOptions` (Core), `WhisperConfig` (Benchmark), and apply it in `WhisperSpeechRecognizer.InitializeAsync(WhisperOptions)` via `builder.WithThreads()`.

**Files:**
- `src/Parlotype.Core/Speech/WhisperOptions.cs` — add `public int? Threads { get; init; }` (null = Whisper default)
- `src/Parlotype.Benchmark/Configuration/BenchmarkConfig.cs` — add `threads` to `WhisperConfig`
- `src/Parlotype.Platform/Speech/WhisperSpeechRecognizer.cs` — apply `builder.WithThreads(options.Threads.Value)` when non-null

### 2. `implement-repetitions` — Execute samples N times and collect per-repetition data

Modify `BenchmarkRunner.RunAsync()` to honor `config.Repetitions`:

- Outer loop: samples. Inner loop: repetitions.
- Collect timing and transcription for each repetition.
- Add `RepetitionDetail` record: `{ int Repetition, double ProcessingTimeMs, double Rtf, double Wer, double Cer, string HypothesisText }`.
- Extend `SampleResult` with:
  - `List<RepetitionDetail> Repetitions` (all individual runs, only populated when repetitions > 1)
  - `double WerStdDev`, `double CerStdDev` (standard deviation across repetitions, 0.0 when repetitions = 1)
- The existing `Wer`, `Cer`, `Rtf`, `ProcessingTimeMs` become the **mean** across repetitions.
- First repetition's `HypothesisText` is used as the representative hypothesis.

**Files:**
- `src/Parlotype.Benchmark/Results/SampleResult.cs` — add `Repetitions`, `WerStdDev`, `CerStdDev` properties
- `src/Parlotype.Benchmark/Results/RepetitionDetail.cs` — new file
- `src/Parlotype.Benchmark/Pipeline/BenchmarkRunner.cs` — implement repetition loop in `ProcessSampleAsync`

### 3. `implement-stability-metrics` — Aggregate stability stats in BenchmarkSummary

Add stability metrics to the summary:

- `double WerStdDev` — average of per-sample WER standard deviations
- `double CerStdDev` — average of per-sample CER standard deviations
- `double WerCoeffOfVariation` — WER stddev / WER mean (as %)
- `int Repetitions` — number of repetitions used

**Files:**
- `src/Parlotype.Benchmark/Results/BenchmarkResult.cs` — add fields to `BenchmarkSummary`
- `src/Parlotype.Benchmark/Pipeline/BenchmarkRunner.cs` — compute stability metrics in summary

### 4. `implement-memory-metrics` — Per-sample RAM and GC tracking

Track memory consumption per sample:

- Before each sample: snapshot `Process.WorkingSet64` and `GC.GetAllocatedBytesForCurrentThread()`
- After each sample: compute delta
- Store in `SampleResult`: `double RamDeltaMb`, `long GcAllocatedBytes`
- In summary: `double AvgRamDeltaMb`, `long TotalGcAllocatedBytes`, GC collection counts per generation

**Files:**
- `src/Parlotype.Benchmark/Results/SampleResult.cs` — add `RamDeltaMb`, `GcAllocatedBytes`
- `src/Parlotype.Benchmark/Results/BenchmarkResult.cs` — add memory fields to `BenchmarkSummary`
- `src/Parlotype.Benchmark/Pipeline/BenchmarkRunner.cs` — add memory snapshot logic around `ProcessSampleAsync`

### 5. `implement-sweep-config` — Sweep configuration model and expansion

Create the sweep config model and Cartesian product expansion:

```csharp
public sealed class SweepConfig
{
    public string Name { get; init; }
    public string? Description { get; init; }
    public string[] Datasets { get; init; }
    public int Repetitions { get; init; } = 1;
    public Dictionary<string, JsonElement[]> Sweep { get; init; }
    public VadConfig Vad { get; init; } = new();
}

public static class SweepExpander
{
    // Expands sweep axes into individual BenchmarkConfigs
    public static List<BenchmarkConfig> Expand(SweepConfig sweep);
}
```

Dot-notation paths supported:
- `whisper.model` → `WhisperConfig.Model`
- `whisper.beamSize` → `WhisperConfig.BeamSize`
- `whisper.temperature` → `WhisperConfig.Temperature`
- `whisper.language` → `WhisperConfig.Language`
- `whisper.threads` → `WhisperConfig.Threads`
- `whisper.initialPrompt` → `WhisperConfig.InitialPrompt`
- `vad.enabled` → `VadConfig.Enabled`

Each expanded config gets a name: `{sweep.Name}-{model}-beam{beamSize}-temp{temperature}-{vad}`.

**Files:**
- `src/Parlotype.Benchmark/Configuration/SweepConfig.cs` — new file
- `src/Parlotype.Benchmark/Configuration/SweepExpander.cs` — new file

### 6. `implement-sweep-command` — CLI command to run parameter sweeps

Add `sweep` command to `Program.cs`:

```
parlotype-bench sweep --config sweep.json --datasets datasets --output results [--verbose]
```

Workflow:
1. Deserialize `SweepConfig` from the config file.
2. Expand into N `BenchmarkConfig` instances via `SweepExpander.Expand()`.
3. Display a summary: "Running sweep: {N} configurations × {M} samples × {R} repetitions".
4. Run each config sequentially (model may need to be reloaded between configs).
5. Save each result as JSON, index in SQLite.
6. After all configs complete, display a summary comparison table of all sweep results.

**Important:** Between sweep runs with different models, the recognizer must be re-initialized. The current `WhisperSpeechRecognizer.InitializeAsync()` checks `if (IsReady) return;` — we need to call `DisposeAsync()` and create a new instance, or add a `ResetAsync()` method. The simplest approach: create a fresh `ServiceProvider` (and thus fresh recognizer) for each sweep configuration.

**Files:**
- `src/Parlotype.Benchmark/Program.cs` — add `sweep` command

### 7. `implement-check-command` — CI regression detection CLI

Add `check` command to `Program.cs`:

```
parlotype-bench check --baseline <run-id> --current <run-id> --output results \
  [--max-wer-delta 2.0] [--max-cer-delta 1.0] [--max-rtf-delta 0.1]
```

Behavior:
- Load both runs via JSON (resolved through SQLite index).
- Compute deltas using `ResultComparer.Compare()`.
- Check each delta against thresholds.
- Print pass/fail summary.
- Return exit code 0 (pass) or 1 (regression detected).

Default thresholds: WER +2.0%, CER +1.0%, RTF +0.1.

**Files:**
- `src/Parlotype.Benchmark/Program.cs` — add `check` command

### 8. `implement-github-actions` — CI workflow for automated benchmarking

Create `.github/workflows/benchmark.yml`:

```yaml
name: Benchmark Regression Check
on:
  pull_request:
    paths:
      - 'src/Parlotype.Core/**'
      - 'src/Parlotype.Platform/**'
  workflow_dispatch:
    inputs:
      baseline_run_id:
        description: 'Baseline run ID to compare against'
        required: true

jobs:
  benchmark:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build Parlotype.slnx
      - run: |
          dotnet run --project src/Parlotype.Benchmark -- run \
            --config datasets/smoke-test-config.json \
            --datasets datasets \
            --output results
      - run: |
          dotnet run --project src/Parlotype.Benchmark -- check \
            --baseline ${{ inputs.baseline_run_id || 'latest' }} \
            --current latest \
            --output results
      - uses: actions/upload-artifact@v4
        if: always()
        with:
          name: benchmark-results
          path: results/
```

**Files:**
- `.github/workflows/benchmark.yml` — new file

### 9. `update-console-reporter` — Display repetition stats and sweep summary

Extend `ConsoleReporter`:
- `DisplayResult()` — show WER/CER stddev columns when repetitions > 1
- `DisplaySweepSummary(List<BenchmarkResult> results)` — table comparing all sweep configurations side-by-side
- Show memory metrics (RAM delta, GC allocations) when available

**Files:**
- `src/Parlotype.Benchmark/Reporting/ConsoleReporter.cs`

### 10. `update-formatters` — Add repetition/memory data to CSV and Markdown

Extend `CsvFormatter` and `MarkdownFormatter`:
- Add stddev columns when repetitions > 1
- Add memory columns (RAM delta, GC bytes) when data present
- `FormatSweepSummary(List<BenchmarkResult>)` for sweep-level export

**Files:**
- `src/Parlotype.Benchmark/Reporting/CsvFormatter.cs`
- `src/Parlotype.Benchmark/Reporting/MarkdownFormatter.cs`

### 11. `implement-phase3-tests` — Unit tests for all Phase 3 functionality

New test files:

**`RepetitionTests.cs`:**
- Verify SampleResult stores N repetition details
- Verify mean WER/CER are computed correctly from repetitions
- Verify stddev calculation

**`SweepExpanderTests.cs`:**
- Expand single-axis sweep produces correct number of configs
- Expand multi-axis sweep produces Cartesian product
- Config naming follows expected pattern
- Invalid dot-notation paths throw descriptive errors

**`MemoryMetricsTests.cs`:**
- RAM delta and GC allocations are stored in SampleResult
- Summary aggregates memory metrics correctly

**`CheckCommandTests.cs`:**
- Pass when deltas are within thresholds
- Fail when WER regression exceeds threshold
- Default thresholds are applied when not specified

**Files:**
- `src/Parlotype.Benchmark.Tests/RepetitionTests.cs`
- `src/Parlotype.Benchmark.Tests/SweepExpanderTests.cs`
- `src/Parlotype.Benchmark.Tests/MemoryMetricsTests.cs`
- `src/Parlotype.Benchmark.Tests/CheckCommandTests.cs`

### 12. `verify-phase3` — Build, test, and smoke-run all new functionality

- `dotnet build Parlotype.slnx` — zero warnings
- `dotnet test` — all tests pass
- Smoke-test:
  - `run` with `repetitions: 3` in config
  - `sweep` with a 2-axis sweep config
  - `check` with pass/fail scenarios

---

## Dependencies Between Todos

```
add-threads-to-whisper (independent)

implement-repetitions (independent)
  └── implement-stability-metrics

implement-memory-metrics (independent)

implement-sweep-config (independent)
  └── implement-sweep-command
        depends on: implement-repetitions (repetitions within sweeps)
        depends on: add-threads-to-whisper (threads as sweep axis)

implement-check-command (independent — uses existing ResultComparer)

implement-github-actions
  depends on: implement-check-command

update-console-reporter
  depends on: implement-repetitions, implement-memory-metrics

update-formatters
  depends on: implement-repetitions, implement-memory-metrics

implement-phase3-tests
  depends on: all implementation todos

verify-phase3
  depends on: all above
```

### Parallelization Waves

**Wave 1 (no dependencies — 5 parallel todos):**
- `add-threads-to-whisper`
- `implement-repetitions`
- `implement-memory-metrics`
- `implement-sweep-config`
- `implement-check-command`

**Wave 2 (depends on Wave 1 — 4 parallel todos):**
- `implement-stability-metrics` (depends on repetitions)
- `implement-sweep-command` (depends on sweep-config + repetitions + threads)
- `implement-github-actions` (depends on check-command)
- `update-console-reporter` (depends on repetitions + memory-metrics)

**Wave 3:**
- `update-formatters` (depends on repetitions + memory-metrics)

**Wave 4:**
- `implement-phase3-tests` (depends on all implementation)
- `verify-phase3` (depends on tests)

---

## Notes

- The `sweep` command creates a **fresh DI container** for each configuration to ensure the Whisper model is properly reloaded when model type changes between sweep runs. This avoids the `if (IsReady) return;` guard in `WhisperSpeechRecognizer.InitializeAsync()`.
- Repetition data is only serialized when `repetitions > 1` to keep JSON files small for single-run benchmarks.
- The `check` command uses a `latest` alias that resolves to the most recent run in the SQLite index.
- GC metrics use `GC.GetAllocatedBytesForCurrentThread()` which is cumulative — we snapshot before and compute delta.
- The GitHub Actions workflow uses `workflow_dispatch` for manual baseline setting and `pull_request` trigger for automated regression checks on code changes.
- Thread count sweeping is useful for finding the optimal parallelism level for a given machine/model combination.
