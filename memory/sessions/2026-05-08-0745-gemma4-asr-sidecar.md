---
title: "Session: 2026-05-08 — Gemma 4 ASR Sidecar"
type: session
status: active
tags: [gemma4, benchmark, sidecar, python]
created: 2026-05-08
summary: "Added Gemma 4 E2B speech recognition to benchmark tool via Python sidecar"
---

# Session: 2026-05-08 — Gemma 4 ASR Sidecar

## Active Focus
- New `Parlotype.Gemma4` project: `Gemma4Config`, `Gemma4SpeechRecognizer`, `sidecar/server.py`
- `BenchmarkConfig` refactored: `Whisper` now nullable, added `Gemma4` block, `EffectiveWhisper`, `EngineName`, `ModelDisplayName`
- `BenchmarkRunner` updated: conditional init path for Whisper vs Gemma4
- `Program.cs` (run command): Gemma4 recognizer creation, disposal, `--gpu false` support
- All reporters/formatters updated: `ConsoleReporter`, `CsvFormatter`, `MarkdownFormatter`, `ResultComparer`, `SqliteResultIndex`
- 9 new tests in `Gemma4ConfigTests.cs`; existing tests updated for nullable Whisper

## Decisions Made
- **ADR-024**: Gemma 4 via Python sidecar (benchmark-only scope)
- Benchmark-only — not wired into Desktop live transcription
- Auto-managed sidecar: spawned on init, health-polled, killed on dispose
- File-based audio transfer (temp WAV files)
- `"gemma4"` config block mutually exclusive with `"whisper"`
- Default quantization changed from `4bit` → `none` (BF16) — bitsandbytes 4-bit crashes on Gemma 4 audio encoder (`torch.finfo()` on uint32)
- Auth token + path restriction for sidecar security; `/shutdown` is unauthenticated for stale cleanup
- Stale sidecar auto-shutdown on port conflicts
- Default dtype `bfloat16`; configurable `dtype` field (bfloat16/float16/float32) for GPU compatibility

## Facts Learned
- `bitsandbytes` 4-bit quantization fails on Gemma 4's audio encoder: `torch.finfo()` called on uint32 weights in `modeling_gemma4.py:427`
- Gemma 4 E2B bfloat16 and float16 on CUDA (RTX 5070 Ti, Blackwell, compute 12.0) produces garbage transcriptions (90%+ WER). CPU inference is correct (11.7% WER)
- float32 model (~20 GB) doesn't fit in 16 GB VRAM
- PyTorch default `pip install torch` installs CPU-only build; need `--index-url https://download.pytorch.org/whl/cu128` for CUDA 12.8
- `Gemma4ForConditionalGeneration` requires `pillow`, `torchvision`, `accelerate` beyond base `transformers` — all added to `requirements.txt`
- HuggingFace CLI is `hf`, not `huggingface-cli`
- `device_map="auto"` with `accelerate` resolves to CUDA when available; previous `"auto"` string went to CPU without accelerate
- `ReadLineAsync()` blocks on progress bar output (carriage returns without newlines); char-buffer `ReadAsync()` fixes it

## Open Blockers
- **Gemma 4 CUDA inference broken on Blackwell GPUs**: bfloat16 and float16 both hallucinate on RTX 5070 Ti. Likely a `transformers` or PyTorch compatibility issue with compute capability 12.0. CPU-only works correctly.
- **bitsandbytes 4-bit quantization incompatible**: `torch.finfo()` crash on Gemma 4 audio encoder layers

## Documentation Status
- ADR: done — `docs/decisions/024-gemma4-python-sidecar.md`
- Vault (services/architecture): done — `memory/services/_index.md`, `memory/services/benchmark.md`, `memory/decisions/_index.md`
- Knowledge (non-derivable facts): done — `memory/knowledge/gemma4-cuda-blackwell.md`

## Next Action
- Monitor `transformers` releases for Blackwell CUDA fix — re-run benchmark with bfloat16 on CUDA once fixed
- Monitor `onnxruntime-genai` issue #2062 for native .NET Gemma 4 support
- Consider Gemma 4 E4B (~4.5B params) benchmark comparison when CUDA works
- Consider adding sweep support for `gemma4.*` axes
