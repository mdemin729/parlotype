# AI Agent Prompt: Implementation Plan for Speech Recognition Quality Benchmark

## Project Context

I am developing **Parlotype** — a privacy-focused voice-to-text application that runs on local AI models (Whisper, Ollama). Tech stack: **.NET / Avalonia UI**, cross-platform (Windows, Linux, macOS).

I need to create a **benchmark (test framework)** for objective evaluation of speech recognition quality. The goal is to have a reproducible, automated tool that enables:

- Comparing different speech recognition pipeline configurations.
- Finding optimal parameters.
- Tracking regressions when code changes.
- Obtaining precise metrics for informed engineering decisions.

---

## Benchmark Requirements

### 1. Test Dataset

- A predefined set of audio tracks with reference transcriptions (ground truth).
- Audio should cover diverse conditions:
    - **Clean speech** (studio quality).
    - **Noisy environments** (background noise, music, conversations).
    - **Varying recording quality** (different sample rates: 8kHz, 16kHz, 44.1kHz, 48kHz; different bitrates).
    - **Different formats** (WAV, MP3, OGG, FLAC).
    - **Different durations** (short phrases 2–5 sec, medium 10–30 sec, long 1–5 min).
    - **Different languages** (minimum: Russian, English; optional: others).
    - **Different accents and speaking styles** (clear, fast, with pauses, with hesitations).
    - **Different domains** (casual speech, technical terminology, dictation).
- Reference storage format: JSON or YAML files with metadata (text, language, duration, recording conditions).

### 2. Quality Metrics

**Required metrics:**

| Metric | Description |
|--------|-------------|
| **WER (Word Error Rate)** | Percentage of word-level errors (standard ASR metric). Formula: `(S + D + I) / N`, where S = substitutions, D = deletions, I = insertions, N = total words in reference. |
| **CER (Character Error Rate)** | Percentage of character-level errors. Useful for languages with long words and for assessing "almost correct" recognitions. |
| **Match Accuracy %** | Simple percentage match between recognized and reference text (for quick-look assessment). |
| **Processing Time** | Absolute time to process an audio file (in milliseconds). |
| **RTF (Real-Time Factor)** | Ratio of processing time to audio duration. RTF < 1.0 = faster than real-time. Key UX metric. |
| **First Result Latency** | Time from processing start to first text fragment. Critical for streaming mode. |

**Desirable metrics:**

| Metric | Description |
|--------|-------------|
| **RAM Consumption** | Peak and average RAM usage during recognition. |
| **VRAM Consumption** | Peak video memory usage (for GPU runtimes). |
| **CPU/GPU Utilization (%)** | Average processor or GPU utilization. |
| **Model Load Time** | Model initialization time (important for cold start). |
| **Result Stability** | Metric variance across multiple runs of the same test (to detect non-determinism). |
| **Punctuation Quality** | Correctness of punctuation marks (if the model generates them). |
| **Capitalization Quality** | Correctness of upper/lower case letters. |

### 3. Input Parameters (Test Run Configuration)

**Model parameters:**

- Selected Whisper model (tiny, base, small, medium, large, large-v2, large-v3, distil-*).
- Model quantization (fp32, fp16, int8, int4).
- Language (auto-detect or explicit).
- Temperature (0.0 — deterministic, 0.2, 0.5, etc.).
- Beam size (1, 2, 5, 10).
- Initial prompt / prefix (contextual hint for the model).
- Suppress tokens (suppression of specific tokens).
- Condition on previous text (true/false).

**Runtime parameters:**

- Target device:
    - CPU (with thread count specification).
    - NVIDIA GPU via CUDA (with specific GPU selection).
    - AMD GPU via Vulkan.
    - Apple Silicon via MLX (future support).
    - ONNX Runtime (CPU/GPU).
- Thread count for CPU inference.
- Batch size (if applicable).

**VAD (Voice Activity Detection) parameters:**

- VAD enable/disable.
- Selected VAD model (Silero VAD, WebRTC VAD, energy-based VAD).
- Sliding window size (in milliseconds).
- Activation threshold.
- Minimum speech segment duration.
- Minimum silence duration for splitting.
- Padding (adding silence around segment edges).

**Audio preprocessing parameters:**

- Volume normalization (on/off, target level).
- Noise reduction (on/off, method, strength).
- Resampling (target sample rate).
- Channel conversion (stereo → mono, method).

### 4. Output Format

- **Console output**: Summary table with key metrics.
- **JSON report**: Full structured report for programmatic processing.
- **CSV export**: For analysis in Excel/Numbers/LibreOffice Calc.
- **Markdown report**: Human-readable report for documentation or README.
- **Comparison report**: Side-by-side comparison of two or more runs with diff metrics (which metrics improved, which degraded, by how much).

### 5. Architectural Requirements

- **Modular architecture**: Easy to plug in new models, runtimes, metrics.
- **File-based configuration**: YAML/JSON files for describing test suites.
- **CLI interface**: Command-line execution with parameters.
- **Reproducibility**: Pinning model versions, seeding random generators, logging full configuration in the report.
- **Regression testing**: Ability to compare current run against a baseline and automatically detect regressions.
- **Incremental execution**: Ability to run only a subset of tests (by language, by duration, by model).
- **Caching**: Don't reload the model between tests if it hasn't changed.

---

## Task

Based on the requirements above, develop a **detailed implementation plan** for the Parlotype benchmark. The plan should include:

1. **Solution architecture**: Component diagram, their interactions, interfaces.
2. **Project structure**: File and folder tree with descriptions of each module's purpose.
3. **Data model**: Structures for test datasets, configurations, results.
4. **Implementation phases**: Breakdown into iterations (MVP → extended version), with specific tasks for each phase.
5. **Technology stack**: Recommendations for libraries and tools (considering the .NET ecosystem).
6. **Test dataset**: Recommendations for free audio datasets for benchmarking (CommonVoice, LibriSpeech, VoxForge, OpenSLR, etc.).
7. **Configuration file examples**: YAML/JSON for describing test scenarios.
8. **Report example**: What a typical benchmark result looks like.
9. **CI/CD integration**: How to embed the benchmark into a pipeline for automatic regression tracking.

**Technical context:**
- Language: C# / .NET 8+
- UI Framework: Avalonia UI (but the benchmark is a console application)
- MVVM: CommunityToolkit.Mvvm
- Logging: ZLogger
- Models: Whisper (via ONNX Runtime, Whisper.net, or native bindings)
- VAD: Silero VAD
- Target platforms: Windows, Linux, macOS
