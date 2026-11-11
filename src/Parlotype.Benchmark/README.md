# Parlotype.Benchmark

Console CLI for evaluating Parlotype's transcription quality. Measures Word Error Rate (WER), Character Error Rate (CER), and Real-Time Factor (RTF) across configurable datasets, engines (Whisper, Gemma 4 via llama.cpp, Parakeet via sherpa-onnx), models and decoding parameters.

Not to be confused with `src/Parlotype.MicroBenchmarks`, which measures allocations and latency of hot paths with BenchmarkDotNet (ADR-044) and is not part of `dotnet test`.

Built with System.CommandLine, Spectre.Console, and SQLite. Results are stored as JSON and auto-indexed into a SQLite database for historical queries and regression detection.

## Quick Start

```bash
# Run a benchmark
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/smoke-test-config.json \
  --datasets datasets --output results

# List past runs
dotnet run --project src/Parlotype.Benchmark -- list --output results

# Compare two runs
dotnet run --project src/Parlotype.Benchmark -- compare \
  --run-a <run-id-a> --run-b <run-id-b> --output results
```

## CLI Commands

### `run` — Execute a benchmark

Runs all samples in the configured datasets through the engine the config selects (see [Configuration](#configuration)) and computes metrics.

| Flag | Required | Default | Description |
|------|----------|---------|-------------|
| `--config <file>` | yes | — | Path to benchmark configuration JSON |
| `--datasets <dir>` | yes | — | Root directory containing dataset folders |
| `--output <dir>` | no | `./results` | Output directory for results |
| `--tags <string>` | no | — | Comma-separated tags to filter samples (AND logic) |
| `--samples <string>` | no | — | Comma-separated sample IDs to include |
| `--gpu <bool>` | no | `true` | Enable GPU acceleration (`--gpu false` for CPU-only). Whisper only: for `llamaCpp` it prints a warning and is ignored (llama-server controls GPU offload itself), and Parakeet is CPU-only anyway |
| `--verbose`, `-v` | no | `false` | Enable verbose logging |

```bash
# Filter by tags and sample IDs
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/smoke-test-config.json \
  --datasets datasets --output results \
  --tags clean,short --samples kennedy

# Force CPU-only
dotnet run --project src/Parlotype.Benchmark -- run \
  --config datasets/smoke-test-config.json \
  --datasets datasets --output results --gpu false
```

### `sweep` — Parameter sweep

Runs a Cartesian product of parameter combinations. Useful for finding optimal model/decoding settings.

| Flag | Required | Default | Description |
|------|----------|---------|-------------|
| `--config <file>` | yes | — | Path to sweep configuration JSON |
| `--datasets <dir>` | yes | — | Root directory containing dataset folders |
| `--output <dir>` | no | `./results` | Output directory for results |
| `--gpu <bool>` | no | `true` | Enable GPU acceleration. `--gpu false` rewrites every expanded config as a Whisper config on `Cpu` |
| `--verbose`, `-v` | no | `false` | Enable verbose logging |

```bash
dotnet run --project src/Parlotype.Benchmark -- sweep \
  --config datasets/sweep-config.json \
  --datasets datasets --output results
```

> **Caveat:** unlike `run`, `sweep` resolves `ISpeechRecognizer` without pinning the engine, so it benchmarks whatever engine your own `settings.json` selects (Parakeet by default). See [`docs/knowledge/benchmark-memory-and-engine-caveats.md`](../../docs/knowledge/benchmark-memory-and-engine-caveats.md) §4.

### `list` — List historical runs

| Flag | Required | Default | Description |
|------|----------|---------|-------------|
| `--output <dir>` | no | `./results` | Directory containing `benchmarks.db` |
| `--model <string>` | no | — | Filter by model name |
| `--config <string>` | no | — | Filter by config name |
| `--last <int>` | no | — | Show only the last N runs |

### `compare` — Compare two runs

| Flag | Required | Default | Description |
|------|----------|---------|-------------|
| `--run-a <string>` | yes | — | Run ID of baseline (A) |
| `--run-b <string>` | yes | — | Run ID of comparison (B) |
| `--output <dir>` | no | `./results` | Directory containing results |
| `--format <string>` | no | `console` | Output format: `console`, `csv`, `markdown`, `json` |

### `export` — Export a run

| Flag | Required | Default | Description |
|------|----------|---------|-------------|
| `--run-id <string>` | yes | — | Run ID to export |
| `--output <dir>` | no | `./results` | Directory containing results |
| `--format <string>` | yes | — | Export format: `csv`, `markdown`, `json` |
| `--file <path>` | no | stdout | Output file path |

```bash
dotnet run --project src/Parlotype.Benchmark -- export \
  --run-id <run-id> --format markdown --output results
```

### `check` — CI regression detection

Compares two runs against configurable thresholds. Returns exit code 0 (pass) or 1 (fail).

| Flag | Required | Default | Description |
|------|----------|---------|-------------|
| `--baseline <string>` | yes | — | Baseline run ID (or `latest`) |
| `--current <string>` | yes | — | Current run ID (or `latest`) |
| `--output <dir>` | no | `./results` | Directory containing results |
| `--max-wer-delta <double>` | no | `2.0` | Max allowed WER increase (percentage points) |
| `--max-cer-delta <double>` | no | `1.0` | Max allowed CER increase (percentage points) |
| `--max-rtf-delta <double>` | no | `0.1` | Max allowed RTF increase |

```bash
dotnet run --project src/Parlotype.Benchmark -- check \
  --baseline <run-id> --current latest \
  --output results --max-wer-delta 2.0
```

### `import` — Rebuild SQLite index

Scans the output directory for JSON result files and rebuilds `benchmarks.db`.

```bash
dotnet run --project src/Parlotype.Benchmark -- import --output results
```

## Configuration

### Benchmark Config (single run)

```json
{
  "name": "smoke-test-baseline",
  "description": "Quick smoke test with Base model and default settings",
  "datasets": ["smoke-test"],
  "repetitions": 1,
  "whisper": {
    "model": "Base",
    "language": "auto",
    "beamSize": 1,
    "temperature": 0.0,
    "initialPrompt": "",
    "threads": 4,
    "runtimePreference": "Auto"
  },
  "vad": {
    "enabled": false,
    "threshold": 0.5,
    "speechPadMs": 400,
    "minSilenceDurationMs": 500,
    "minSpeechDurationMs": 50,
    "interSegmentSilenceMs": 160
  }
}
```

#### Whisper options

| Field | Default | Description |
|-------|---------|-------------|
| `model` | `Base` | Whisper model: `Tiny`, `TinyEn`, `Base`, `BaseEn`, `Small`, `SmallEn`, `Medium`, `MediumEn` |
| `language` | `auto` | Language code (e.g. `en`) or `auto` for detection |
| `beamSize` | `1` | Beam search width. `1` = greedy decoding, `>1` = beam search |
| `temperature` | `0.0` | Sampling temperature |
| `initialPrompt` | — | Optional prompt to bias transcription |
| `threads` | — | CPU thread count (omit to use Whisper default) |
| `runtimePreference` | `Auto` | Runtime: `Auto` (Vulkan, then CPU), `Vulkan` (strict, fails if unavailable), `Cpu` |

#### Gemma 4 options (alternative engine)

Instead of `"whisper"`, use a `"llamaCpp"` block to run Gemma 4 E2B/E4B via the llama.cpp
server. Engine blocks are mutually exclusive — provide one of `whisper`, `llamaCpp`, `parakeet`.

```json
{
  "name": "gemma4-smoke-test",
  "description": "Gemma 4 E4B Q4_K_M smoke test via llama.cpp",
  "datasets": ["smoke-test"],
  "llamaCpp": {
    "modelId": "gemma-4-E4B-it-Q4_K_M",
    "port": 8321
  },
  "vad": { "enabled": false }
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `modelId` | `gemma-4-E4B-it-Q4_K_M` | Gemma 4 GGUF catalog ID. Valid IDs (case-sensitive): `gemma-4-E2B-it-Q8_0`, `gemma-4-E2B-it-bf16`, `gemma-4-E4B-it-Q4_K_M`, `gemma-4-E4B-it-Q8_0`, `gemma-4-E4B-it-bf16` |
| `port` | `8321` | Localhost port for the llama-server process |
| `serverFolder` | auto | Optional path to the folder containing `llama-server.exe`. When omitted, the active managed install from the llama-server registry is used; with no managed install and no `serverFolder`, the run fails with "No llama-server is configured" |

**Prerequisites:**
1. llama-server installed via **Settings → llama.cpp** in the Parlotype Desktop app
2. Gemma 4 GGUF model downloaded via **Settings → Gemma 4 model** in the Desktop app
   (or downloaded manually to `%LOCALAPPDATA%\parlotype-data\models\`)

The benchmark adopts an already-running llama-server on the configured port, or spawns
a new one automatically. Transcription uses the OpenAI-compatible `/v1/chat/completions`
endpoint with `input_audio` content blocks.

See [ADR-029](../../docs/decisions/029-gemma4-model-download-ui.md) for design rationale.

#### Parakeet options (alternative engine)

A `"parakeet"` block selects NVIDIA Parakeet TDT v3 via sherpa-onnx, the app's default
engine ([ADR-041](../../docs/decisions/041-parakeet-v3-sherpa-onnx.md)). It is CPU-only and
always auto-detects the language. Sample config: `datasets/parakeet-smoke-config.json`.

```json
{
  "name": "smoke-test-parakeet",
  "datasets": ["smoke-test"],
  "parakeet": { "modelId": "parakeet-tdt-0.6b-v3-int8" },
  "vad": { "enabled": false }
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `modelId` | `parakeet-tdt-0.6b-v3-int8` | Parakeet catalog ID: `parakeet-tdt-0.6b-v3-int8` or `parakeet-tdt-0.6b-v3-fp32` |

The model is downloaded headlessly on first use when it is not already cached.

#### Engine selection and your own settings

`whisper`, `llamaCpp` and `parakeet` are mutually exclusive; with none of them the run uses
Whisper defaults. `run` never reads the engine or model from your `settings.json`: it
registers a benchmark-scoped `InMemorySettingsService` holding the config's values and
resolves the concrete recognizer (`WhisperSpeechRecognizer`, `LlamaCppSpeechRecognizer` or
`ParakeetSpeechRecognizer`) directly, bypassing `DelegatingSpeechRecognizer`. `sweep` does
not do this yet (see the caveat under [`sweep`](#sweep--parameter-sweep)).

#### VAD options

| Field | Default | Description |
|-------|---------|-------------|
| `enabled` | `true` | Run Silero VAD to extract speech segments before transcription |
| `threshold` | `0.5` | Speech probability threshold (0.0–1.0) |
| `speechPadMs` | `400` | Padding around detected speech segments (ms) |
| `minSilenceDurationMs` | `500` | Minimum silence to split segments (ms) |
| `minSpeechDurationMs` | `50` | Minimum speech duration to keep (ms) |
| `interSegmentSilenceMs` | `160` | Silence inserted between concatenated segments (ms) |
| `silenceThresholdMs` | `null` | Pipeline flush silence threshold (ms). When set, simulates real-time AudioPipelineService behavior: audio is fed in 10ms callbacks, VAD runs in 500ms chunks, and silence exceeding this threshold triggers a flush. Each flush segment is transcribed separately and results are concatenated. When null, the entire file is processed in one shot (existing behavior). |

### Sweep Config (parameter sweep)

Define axes of parameters to sweep. The runner generates the Cartesian product of all axis values.

```json
{
  "name": "model-beam-sweep",
  "description": "Sweep across models and beam sizes",
  "datasets": ["smoke-test"],
  "repetitions": 2,
  "sweep": {
    "whisper.model": ["Base", "Small"],
    "whisper.beamSize": [1, 5],
    "whisper.temperature": [0.0, 0.2],
    "vad.enabled": [false, true]
  },
  "vad": {
    "enabled": false
  }
}
```

The example above produces 2 x 2 x 2 x 2 = 16 configurations. Each runs all dataset samples with 2 repetitions.

**Supported sweep axes** (dot-notation paths):

| Axis | Type | Example values |
|------|------|----------------|
| `whisper.model` | string | `["Tiny", "Base", "Small"]` |
| `whisper.beamSize` | int | `[1, 3, 5]` |
| `whisper.temperature` | float | `[0.0, 0.2]` |
| `whisper.language` | string | `["auto", "en"]` |
| `whisper.threads` | int | `[4, 8]` |
| `whisper.initialPrompt` | string | `["prompt1", "prompt2"]` |
| `whisper.runtimePreference` | string | `["Auto", "Cpu"]` |
| `vad.enabled` | bool | `[true, false]` |
| `vad.threshold` | float | `[0.3, 0.5, 0.7]` |
| `vad.speechPadMs` | int | `[200, 400]` |
| `vad.minSilenceDurationMs` | int | `[300, 500]` |
| `vad.minSpeechDurationMs` | int | `[50, 100]` |
| `vad.interSegmentSilenceMs` | int | `[0, 160]` |
| `vad.silenceThresholdMs` | int | `[100, 500, 3000]` |

### Pipeline Simulation Mode

The `silenceThresholdMs` parameter enables pipeline simulation mode, which reproduces the real-time behavior of `AudioPipelineService` during benchmarking. This is useful for understanding how silence-triggered segment boundaries affect transcription quality in production.

**How it works:** When `silenceThresholdMs` is set to a value (e.g., 500), the benchmark simulates the live pipeline by feeding audio in 10ms callbacks, running VAD on 500ms chunks, and flushing accumulated audio whenever silence exceeds the threshold. Each flush produces a segment that is transcribed independently; results are then concatenated with inter-segment silence inserted between them. This can differ from processing the entire file at once, since Whisper context and sentence-level output formatting are reset on each flush. By sweeping `silenceThresholdMs` values, you can identify optimal thresholds that balance latency (shorter thresholds = faster user response) with transcription quality (longer thresholds = more context per segment).

When `silenceThresholdMs` is null, the file is processed in one shot (existing default behavior), suitable for batch evaluation where latency is not a concern.

## Datasets

Datasets live in the directory passed via `--datasets`. Each dataset is a subfolder containing a `manifest.json` and a `samples/` directory with audio files.

### Directory layout

```
datasets/
  smoke-test/
    manifest.json
    samples/
      61-70968-0000.flac
      237-126133-0000.flac
      Russian-accent--with-pauses-001.flac
  libri-speech-test-other/
    manifest.json
    samples/
      ...
```

### Manifest format

```json
{
  "name": "smoke-test",
  "description": "Minimal smoke test dataset",
  "language": "en",
  "samples": [
    {
      "id": "sample-001",
      "file": "samples/sample-001.flac",
      "referenceText": "The ground truth transcription.",
      "language": "en",
      "durationSeconds": 4.2,
      "tags": ["clean", "English", "short"]
    }
  ]
}
```

| Field | Required | Description |
|-------|----------|-------------|
| `id` | yes | Unique sample identifier (used for filtering with `--samples`) |
| `file` | yes | Relative path to audio file inside the dataset folder |
| `referenceText` | yes | Ground truth transcription for WER/CER computation |
| `language` | no | Language override (falls back to dataset-level `language`) |
| `durationSeconds` | no | Audio duration in seconds (informational) |
| `tags` | no | Tags for filtering with `--tags` (AND logic: sample must have all specified tags) |

**Audio formats:** WAV files are loaded directly. FLAC files are converted to WAV via FFmpeg (16 kHz, mono, 16-bit PCM) and cached in a `.cache/` folder.

## Metrics

All metrics are computed after text normalization (lowercase, punctuation removal, whitespace collapse).

| Metric | Description |
|--------|-------------|
| **WER** | Word Error Rate — `(substitutions + deletions + insertions) / reference_words * 100`. Lower is better. |
| **CER** | Character Error Rate — same formula at the character level. Lower is better. |
| **RTF** | Real-Time Factor — `processing_time / audio_duration`. Values < 1.0 mean faster than real-time. |

When `repetitions > 1`, the runner also computes per-sample standard deviation and coefficient of variation for WER/CER to assess stability.

Memory metrics are tracked per-sample: peak RAM (MB), average RAM delta, total GC allocations, and GC collection counts per generation.

### Warm-up pass

Every run performs **one throwaway transcription of the first sample** between `InitializeAsync` and the timed loop. This primes the GPU runtime / OS page cache / first-inference paths so the reported per-sample numbers reflect steady-state behaviour rather than cold-start cost. The time spent in warm-up is reported separately as `warmupTimeMs`; the same sample is re-run inside the timed loop. GC counters are reset after warm-up so per-sample GC numbers cover the timed loop only. See [ADR-071](../../docs/decisions/071-benchmark-warmup-pass.md).

> **Caveat:** `peakRamMb` is derived from `Process.PeakWorkingSet64`, which is a high-water mark — warm-up RAM may be reflected in peak readings during the timed loop. For Whisper this is a non-issue because the model is already loaded by `InitializeAsync`. For Gemma the model lives in an external `llama-server` process and isn't counted either way.

## Results Storage

Each run produces a JSON file in the output directory (e.g. `20260319-145230-smoke-test-baseline.json`) and is auto-indexed into `benchmarks.db` (SQLite) for fast historical queries.

The SQLite index supports partial run ID matching — you don't need to type the full ID for `compare`, `export`, or `check` commands.

## Project Structure

```
Parlotype.Benchmark/
  Program.cs              # CLI entry point and command definitions
  Configuration/          # Config and manifest deserialization
  Metrics/                # WER/CER computation, text normalization
  Pipeline/               # BenchmarkRunner, audio loading
  Results/                # Result models, JSON store, SQLite index
  Reporting/              # Console, CSV, Markdown formatters, comparison engine
```

## Contributing

- All console output goes through Spectre.Console (`AnsiConsole`), never `Console.WriteLine`. Pass any user- or file-supplied string through `Markup.Escape()` before embedding it in markup.
- The benchmark is a pure console app: it references Platform and Core, never `Parlotype.Desktop` or Avalonia.
- Load the model once per run and time model load separately from per-sample transcription.
- Tests live in `src/Parlotype.Benchmark.Tests` (xUnit v2): metrics, normalization, config deserialization, sweep expansion, comparison, formatters, SQLite index and regression checks.
