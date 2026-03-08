---
title: Benchmark CLI
status: in_progress
created: 2026-03-04
started: 2026-03-04
completed:
---

# Parlotype Benchmark — Requirements

## 1. Goal

Build a **console benchmark application** (`Parlotype.Benchmark`) for objective, reproducible evaluation of the speech recognition pipeline. The benchmark enables:

- Comparing different pipeline configurations (models, VAD settings, runtimes).
- Finding optimal parameters for quality/performance trade-offs.
- Tracking regressions when code changes.
- Producing precise metrics for engineering decisions.

---

## 2. Input Data (Test Dataset)

### 2.1 Audio Format

- **WAV** (16-bit PCM) — loaded natively via NAudio.
- **FLAC** — converted to WAV at runtime via FFmpeg (`ffmpeg` must be on PATH). Converted files are cached in a `.cache` directory next to the source. No MP3 or OGG support.
- The benchmark must handle audio files at various sample rates (8kHz, 16kHz, 44.1kHz, 48kHz) — resampling to 16kHz mono internally, matching the existing `AudioFormat.Whisper` pipeline.

### 2.2 Dataset Structure

A directory-based dataset with ground-truth transcriptions:

```
datasets/
  librispeech-clean/
    manifest.json          # Dataset metadata + per-sample ground truth
    samples/
      sample-001.wav
      sample-002.wav
      ...
  noisy-speech/
    manifest.json
    samples/
      ...
```

### 2.3 Manifest Schema (`manifest.json`)

```json
{
  "name": "librispeech-clean",
  "description": "Clean English speech from LibriSpeech test-clean",
  "language": "en",
  "samples": [
    {
      "id": "sample-001",
      "file": "samples/sample-001.wav",
      "referenceText": "the quick brown fox jumps over the lazy dog",
      "language": "en",
      "durationSeconds": 4.2,
      "tags": ["clean", "short", "english"]
    }
  ]
}
```

### 2.4 Audio Diversity

The dataset should cover:

| Dimension | Variations |
|-----------|-----------|
| **Recording quality** | Clean (studio), noisy (background noise, music) |
| **Sample rates** | 8kHz, 16kHz, 44.1kHz, 48kHz (all resampled internally) |
| **Duration** | Short (2–5 s), medium (10–30 s), long (1–5 min) |
| **Languages** | English (MVP), Russian (phase 2), others (optional) |
| **Speaking styles** | Clear, fast, with pauses, with hesitations |
| **Domains** | Casual speech, technical terminology, dictation |

### 2.5 Recommended Free Datasets

| Dataset | Language | Description |
|---------|----------|-------------|
| **LibriSpeech** (test-clean, test-other) | English | Standard ASR benchmark, clean & noisy subsets |
| **CommonVoice** (Mozilla) | Multi | Crowd-sourced, diverse accents |
| **VoxForge** | Multi | Open-source speech corpus |
| **OpenSLR** | Multi | Collection of freely available speech resources |
| **Golos** | Russian | Open Russian speech dataset |

---

## 3. Pipeline Parameters (Run Configuration)

Parameters to vary across benchmark runs. Values in **bold** are the current Parlotype defaults.

### 3.1 Whisper Model Parameters

| Parameter | Values | Notes |
|-----------|--------|-------|
| Model | Tiny, TinyEn, Base, **BaseEn**, Small, SmallEn, Medium, MediumEn, LargeV1, LargeV2, LargeV3, LargeV3Turbo | Maps to existing `WhisperModelType` enum |
| Language | **"auto"**, explicit code (e.g. "en", "ru") | Currently hardcoded to "auto" in `WhisperSpeechRecognizer` |
| Beam size | **1** (greedy), 2, 5, 10 | Whisper.net `BeamSize` parameter |
| Temperature | **0.0** (deterministic), 0.2, 0.5 | Whisper.net `Temperature` parameter |
| Initial prompt | empty, custom string | Context hint for domain-specific vocabulary |

### 3.2 Runtime Parameters

| Parameter | Values | Notes |
|-----------|--------|-------|
| Device | **CPU**, CUDA, Vulkan | Selects Whisper.net runtime package |
| CPU thread count | 1, 2, 4, **auto** | `WhisperProcessorBuilder.WithThreads()` |

### 3.3 VAD Parameters (Silero VAD)

| Parameter | Default | Range | Notes |
|-----------|---------|-------|-------|
| VAD enabled | **true** | true/false | Skip VAD = feed entire audio to Whisper |
| Threshold | **0.5** | 0.1–0.9 | Speech detection confidence |
| Min speech duration (ms) | **50** | 20–500 | Minimum speech segment |
| Min silence duration (ms) | **300** | 100–1000 | Silence gap to split segments |
| Window size (samples) | **1024** | 512–2048 | Processing window |
| Speech padding (ms) | **100** | 0–500 | Padding around detected speech |

### 3.4 Audio Preprocessing

| Parameter | Default | Notes |
|-----------|---------|-------|
| Volume normalization | **off** | Future: target dBFS level |
| Noise reduction | **off** | Future: algorithm + strength |
| Resampling target | **16kHz** | Always applied (matches Whisper input) |
| Channel conversion | **mono** | Always applied |

### 3.5 Run Configuration Schema

```json
{
  "name": "baseline-base-en-cpu",
  "description": "Baseline run with Base English model on CPU",
  "datasets": ["librispeech-clean"],
  "repetitions": 3,
  "whisper": {
    "model": "BaseEn",
    "language": "auto",
    "beamSize": 1,
    "temperature": 0.0,
    "initialPrompt": ""
  },
  "runtime": {
    "device": "CPU",
    "threads": 4
  },
  "vad": {
    "enabled": true,
    "threshold": 0.5,
    "minSpeechDurationMs": 50,
    "minSilenceDurationMs": 300,
    "windowSizeSamples": 1024,
    "speechPadMs": 100
  },
  "preprocessing": {
    "normalize": false,
    "noiseReduction": false
  }
}
```

---

## 4. Metrics

### 4.1 Required Metrics (MVP)

| Metric | Description | Unit |
|--------|-------------|------|
| **WER** (Word Error Rate) | `(S + D + I) / N` — substitutions, deletions, insertions over total reference words | % |
| **CER** (Character Error Rate) | Character-level edit distance / reference character count | % |
| **RTF** (Real-Time Factor) | Processing time / audio duration. RTF < 1.0 = faster than real-time | ratio |
| **Processing time** | Wall-clock time to transcribe one audio file | ms |
| **Model load time** | Time to initialize the Whisper model | ms |
| **Peak RAM** | Peak working set during inference | MB |

### 4.2 Desirable Metrics (Phase 2)

| Metric | Description | Unit |
|--------|-------------|------|
| Peak VRAM | GPU memory during inference (CUDA/Vulkan) | MB |
| CPU/GPU utilization | Average utilization during inference | % |
| Result stability | Variance of WER across repeated runs of the same sample | σ |
| Punctuation accuracy | F1 score for punctuation marks | % |
| Capitalization accuracy | F1 score for case correctness | % |
| First result latency | Time to first text fragment (streaming mode) | ms |

### 4.3 WER/CER Calculation

Use **Levenshtein distance** (edit distance) at word and character level. Before comparison, apply text normalization:

1. Lowercase both reference and hypothesis.
2. Remove punctuation (configurable — can be kept for punctuation metric).
3. Collapse multiple spaces.
4. Optionally expand common contractions.

Implementation options:
- **Custom implementation** — straightforward dynamic programming, no external dependency.
- **NuGet: `Fastenshtein`** — fast Levenshtein distance (character-level); word-level needs tokenization first.
- Port the Python `jiwer` algorithm (standard in ASR research) to C#.

---

## 5. Results Storage

### 5.1 Recommendation: **Hybrid — JSON files + SQLite index**

| Aspect | JSON Files | SQLite Database |
|--------|-----------|-----------------|
| Human-readable | ✅ Yes | ❌ No |
| Version-controllable | ✅ Yes (git-friendly) | ⚠️ Binary diffs |
| Queryable | ❌ Manual parsing | ✅ SQL queries |
| Cross-run comparison | ❌ Need custom code | ✅ Simple JOINs |
| Portable | ✅ Copy anywhere | ✅ Single file |
| Schema evolution | ⚠️ Loose | ✅ Migrations |

**Recommended approach:**

1. **Primary storage: JSON files** — one file per benchmark run, stored under a `results/` directory. Human-readable, git-friendly, easy to share. File naming: `{timestamp}-{config-name}.json`.

2. **Index/query layer: SQLite** — a single `benchmarks.db` file that indexes all JSON results for fast querying and comparison. Rebuilt from JSON files on demand (`benchmark import` command).

This gives the best of both worlds: JSON for portability/readability, SQLite for analytics.

### 5.2 Result File Schema

```json
{
  "runId": "20260304-223000-baseline-base-en-cpu",
  "timestamp": "2026-03-04T22:30:00Z",
  "configuration": { /* full run config snapshot */ },
  "environment": {
    "os": "Windows 11",
    "cpu": "AMD Ryzen 9 7950X",
    "ram": "64 GB",
    "gpu": "NVIDIA RTX 4090",
    "dotnetVersion": "10.0.0",
    "whisperNetVersion": "1.9.0"
  },
  "summary": {
    "totalSamples": 50,
    "averageWer": 5.2,
    "averageCer": 2.1,
    "averageRtf": 0.35,
    "totalProcessingTimeMs": 12500,
    "modelLoadTimeMs": 850,
    "peakRamMb": 1200
  },
  "samples": [
    {
      "id": "sample-001",
      "referenceText": "the quick brown fox",
      "hypothesisText": "the quick brown box",
      "wer": 25.0,
      "cer": 5.3,
      "processingTimeMs": 250,
      "rtf": 0.06,
      "peakRamMb": 1150
    }
  ]
}
```

### 5.3 SQLite Schema (Index)

```sql
CREATE TABLE runs (
    run_id TEXT PRIMARY KEY,
    timestamp TEXT NOT NULL,
    config_name TEXT,
    model TEXT,
    device TEXT,
    vad_enabled INTEGER,
    avg_wer REAL,
    avg_cer REAL,
    avg_rtf REAL,
    model_load_time_ms REAL,
    peak_ram_mb REAL,
    json_path TEXT
);

CREATE TABLE sample_results (
    run_id TEXT REFERENCES runs(run_id),
    sample_id TEXT,
    wer REAL,
    cer REAL,
    processing_time_ms REAL,
    rtf REAL,
    PRIMARY KEY (run_id, sample_id)
);
```

---

## 6. Benchmark Comparison Tool

### 6.1 Recommendation: **Built-in CLI `compare` command**

Rather than relying on an external tool, build comparison directly into the benchmark CLI. This keeps the workflow self-contained and .NET-native.

**Commands:**

```bash
# Run a benchmark
parlotype-bench run --config baseline.json

# List all stored runs
parlotype-bench list [--model Base] [--last 10]

# Compare two runs
parlotype-bench compare <run-id-1> <run-id-2>

# Compare against a named baseline
parlotype-bench compare --baseline baseline-v1 --current <run-id>

# Export comparison to markdown
parlotype-bench compare <run-id-1> <run-id-2> --format md > comparison.md
```

**Comparison output (console):**

```
┌──────────────────┬────────────┬────────────┬──────────┐
│ Metric           │ Run A      │ Run B      │ Δ        │
├──────────────────┼────────────┼────────────┼──────────┤
│ Model            │ Base       │ Small      │ -        │
│ Avg WER          │ 8.5%       │ 5.2%       │ -3.3% ✅ │
│ Avg CER          │ 3.1%       │ 2.0%       │ -1.1% ✅ │
│ Avg RTF          │ 0.25       │ 0.55       │ +0.30 ⚠️ │
│ Model Load (ms)  │ 450        │ 1200       │ +750  ⚠️ │
│ Peak RAM (MB)    │ 800        │ 1500       │ +700  ⚠️ │
└──────────────────┴────────────┴────────────┴──────────┘
```

### 6.2 Export Formats

| Format | Use Case |
|--------|----------|
| **Console table** | Quick interactive comparison |
| **JSON** | Programmatic consumption, CI/CD |
| **CSV** | Analysis in Excel / Google Sheets / Python pandas |
| **Markdown** | Documentation, PR descriptions, README |

### 6.3 External Tools (Optional, for deeper analysis)

If deeper analysis is needed beyond the built-in compare:

- **Export CSV → Python + pandas + matplotlib** — for charts, trend lines, statistical analysis.
- **Export CSV → Excel / Google Sheets** — for ad-hoc exploration with pivot tables.
- **Grafana + SQLite datasource** — for dashboards if continuous benchmarking is set up in CI.

---

## 7. Architectural Requirements

### 7.1 Project Structure

```
src/
  Parlotype.Benchmark/          # New console app project
    Program.cs                  # CLI entry point (System.CommandLine)
    Commands/
      RunCommand.cs             # Execute benchmark run
      ListCommand.cs            # List stored runs
      CompareCommand.cs         # Compare runs
      ImportCommand.cs          # Rebuild SQLite index from JSON files
    Configuration/
      BenchmarkConfig.cs        # Run configuration model
      DatasetManifest.cs        # Dataset manifest model
    Metrics/
      IMetricsCalculator.cs     # Interface for WER/CER calculation
      LevenshteinMetrics.cs     # Edit-distance based WER/CER
      PerformanceMetrics.cs     # RTF, timing, memory collection
    Pipeline/
      IBenchmarkRunner.cs       # Orchestrates a benchmark run
      BenchmarkRunner.cs        # Implementation
      AudioFileLoader.cs        # Load + resample WAV files
      TextNormalizer.cs         # Normalize text before comparison
    Results/
      BenchmarkResult.cs        # Result data model
      JsonResultStore.cs        # Save/load JSON result files
      SqliteResultIndex.cs      # SQLite index for querying
      ResultComparer.cs         # Diff two runs
    Reporting/
      IReportFormatter.cs       # Format results for output
      ConsoleTableFormatter.cs
      JsonReportFormatter.cs
      CsvReportFormatter.cs
      MarkdownReportFormatter.cs
```

### 7.2 Key Design Principles

1. **Reuse existing Parlotype infrastructure** — `ISpeechRecognizer`, `IVadService`, `WhisperSpeechRecognizer`, `SileroVadService`, audio resampling from `Parlotype.Platform`.
2. **Dependency direction** — `Parlotype.Benchmark → Parlotype.Platform → Parlotype.Core`.
3. **Modular metrics** — easy to add new metrics without modifying the runner.
4. **Configuration-driven** — JSON config files for reproducible runs.
5. **CLI interface** — no UI dependency; use `System.CommandLine` for argument parsing.
6. **Reproducibility** — log full configuration + environment info in every result file.
7. **Caching** — don't reload the Whisper model between samples within a single run.
8. **Incremental runs** — filter by dataset, tags, or sample IDs via CLI flags.

### 7.3 Dependency on Existing Code

The benchmark reuses these existing components directly:

| Component | Source | Usage in Benchmark |
|-----------|--------|--------------------|
| `WhisperSpeechRecognizer` | Platform/Speech | Transcribe audio samples |
| `SileroVadService` | Platform/Audio | Optional VAD preprocessing |
| `WhisperModelType` | Core/Speech | Model selection enum |
| `WhisperModelInfo` | Core/Speech | Model metadata (sizes, SHA) |
| `IModelDownloadService` | Core/Speech | Download models if missing |
| `AudioFormat.Whisper` | Core/Audio | Target format (16kHz mono) |
| `ISettingsService` | Core/Settings | Read/write model preferences |

New parameters (beam size, temperature, language) will require exposing additional configuration on `WhisperSpeechRecognizer` or creating a benchmark-specific recognizer wrapper.

---

## 8. Implementation Phases

### Phase 1 — MVP

- Single dataset, single configuration, WER + CER + RTF + processing time.
- JSON result output.
- Console summary table.
- Manual WAV dataset with a few samples + ground truth.

### Phase 2 — Comparison & Storage

- SQLite index for historical runs.
- `compare` command with delta display.
- CSV/Markdown export.
- Multiple datasets and tag-based filtering.

### Phase 3 — Extended Metrics & Parameters

- RAM/VRAM measurement.
- Model load time tracking.
- Beam size, temperature, language parameter sweeps.
- Result stability across repeated runs.
- CI/CD integration (regression detection against baseline).

### Phase 4 — Advanced

- Punctuation and capitalization metrics.
- Streaming mode (first result latency).
- GPU-specific runtimes (CUDA, Vulkan).
- Automated parameter grid search.
- Grafana/dashboard integration.

---

## 9. Technology Stack

| Component | Technology |
|-----------|-----------|
| CLI framework | `System.CommandLine` (Microsoft) |
| Speech recognition | `Whisper.net` 1.9.0 (existing) |
| VAD | `SileroVad` 1.3.0 (existing) |
| Audio loading | `NAudio` 2.2.1 (existing) — WAV reading + resampling |
| WER/CER | Custom Levenshtein implementation (no external dependency) |
| Results storage | JSON (`System.Text.Json`) + SQLite (`Microsoft.Data.Sqlite`) |
| Console output | `Spectre.Console` (rich tables, progress bars) |
| Logging | `ZLogger` (existing) |
| Testing | `xUnit` (existing) |
