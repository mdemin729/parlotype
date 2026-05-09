# Parlotype.Benchmark

Console CLI for evaluating Parlotype's transcription quality. Measures Word Error Rate (WER), Character Error Rate (CER), and Real-Time Factor (RTF) across configurable datasets, Whisper models, and decoding parameters.

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

Runs all samples in the configured datasets through the Whisper pipeline and computes metrics.

| Flag | Required | Default | Description |
|------|----------|---------|-------------|
| `--config <file>` | yes | — | Path to benchmark configuration JSON |
| `--datasets <dir>` | yes | — | Root directory containing dataset folders |
| `--output <dir>` | no | `./results` | Output directory for results |
| `--tags <string>` | no | — | Comma-separated tags to filter samples (AND logic) |
| `--samples <string>` | no | — | Comma-separated sample IDs to include |
| `--gpu <bool>` | no | `true` | Enable GPU acceleration (`--gpu false` for CPU-only) |
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
| `--gpu <bool>` | no | `true` | Enable GPU acceleration |
| `--verbose`, `-v` | no | `false` | Enable verbose logging |

```bash
dotnet run --project src/Parlotype.Benchmark -- sweep \
  --config datasets/sweep-config.json \
  --datasets datasets --output results
```

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
| `runtimePreference` | `Auto` | Runtime: `Auto`, `Cpu`, `Gpu` |

#### Gemma 4 options (alternative engine)

Instead of `"whisper"`, use a `"gemma4"` block to run Gemma 4 E2B/E4B via a Python sidecar.
The two blocks are mutually exclusive — provide one or the other.

```json
{
  "name": "gemma4-smoke-test",
  "description": "Gemma 4 E2B smoke test (4-bit quantized)",
  "datasets": ["smoke-test"],
  "gemma4": {
    "modelId": "gemma-4-E2B-it",
    "quantization": "4bit",
    "port": 8321
  },
  "vad": { "enabled": false }
}
```

| Field | Default | Description |
|-------|---------|-------------|
| `modelId` | `gemma-4-E2B-it` | HuggingFace model name (`gemma-4-E2B-it` or `gemma-4-E4B-it`) |
| `modelPath` | auto | Local path to pre-downloaded model. Defaults to `{LocalAppData}/parlotype/models/{modelId}` |
| `quantization` | `none` | `none` (BF16 full precision), `4bit` (bitsandbytes), or `8bit`. Note: 4-bit/8-bit may fail on Gemma 4 audio encoder |
| `port` | `8321` | Localhost port for the Python sidecar |
| `pythonPath` | `python` | Path to the Python executable |
| `maxNewTokens` | `200` | Maximum tokens generated per transcription |
| `deviceMap` | `auto` | Torch device placement (`auto`, `cpu`, `cuda:0`) |
| `startupTimeoutSeconds` | `180` | Timeout for sidecar startup and model loading |

**Prerequisites:**
1. Python 3.10+ with CUDA support
2. Install dependencies: `pip install -r src/Parlotype.Gemma4/sidecar/requirements.txt`
3. Pre-download the model:
   ```bash
   hf login
   hf download google/gemma-4-E2B-it \
     --local-dir "%LOCALAPPDATA%\parlotype\models\gemma-4-E2B-it"
   ```

See [ADR-024](../../docs/decisions/024-gemma4-python-sidecar.md) for design rationale.

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
