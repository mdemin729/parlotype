# ADR-024: Gemma 4 Speech Recognition via Python Sidecar

## Status

Accepted

## Context

Parlotype needs to benchmark Gemma 4 (E2B/E4B) for automatic speech recognition alongside
Whisper. Gemma 4's novel architecture (Per-Layer Embeddings, variable head dimensions,
KV cache sharing) is not yet supported by `onnxruntime-genai` (tracked in
[microsoft/onnxruntime-genai#2062](https://github.com/microsoft/onnxruntime-genai/issues/2062)),
so native .NET inference is not possible today.

Three paths were evaluated:

1. **Python sidecar** — local FastAPI server using HuggingFace Transformers
2. **Raw ONNX Runtime** — manual inference loop with split ONNX models
3. **Cloud API** — Azure AI Foundry

## Decision

Use a **Python sidecar** (Option 1) for the benchmark tool. A new `Parlotype.Gemma4` project
contains:

- `Gemma4SpeechRecognizer` implementing `ISpeechRecognizer` — manages the Python process
  lifecycle, writes temp WAV files, and communicates over HTTP to `127.0.0.1`
- `Gemma4Config` — benchmark JSON configuration (`"gemma4": { ... }` block)
- `sidecar/server.py` — FastAPI server with `/transcribe` and `/health` endpoints

The sidecar is **auto-managed**: spawned on `InitializeAsync()`, health-polled until ready,
and killed on `DisposeAsync()`. An auth token and path restriction prevent local file disclosure.

Quantization is configurable (4-bit via bitsandbytes, 8-bit, or BF16 full precision).
4-bit is the default, requiring ~3 GB disk and 6–8 GB VRAM for E2B.

## Scope

**Benchmark-only.** The sidecar is not wired into the Desktop app's live transcription
pipeline. Production integration should wait for native .NET support via `onnxruntime-genai`.

## Consequences

- Python + CUDA must be installed to use Gemma 4 benchmarks
- `bitsandbytes` has limited Windows support — users may need WSL2 or Linux for quantized mode
- Benchmark results include HTTP/file transfer overhead (labeled as such)
- Model must be pre-downloaded by the user (`hf download`)
- The `BenchmarkConfig.Whisper` property is now nullable; `EffectiveWhisper` provides defaults
- When `onnxruntime-genai` adds Gemma 4 support, the sidecar can be replaced with a native
  implementation without changing the `ISpeechRecognizer` interface
