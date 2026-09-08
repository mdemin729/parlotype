---
title: "Session: Parakeet v3 engine research & plan"
type: session
status: completed
tags: [research, planning, parakeet, speech-engine]
created: 2026-07-06
completed: 2026-07-06
summary: "Researched NVIDIA Parakeet TDT 0.6B v3 as a third speech engine and produced a 3-document plan (task, research, implementation) with sherpa-onnx .NET bindings as the runtime."
---

# Session: Parakeet v3 engine research & plan

## Active Focus

- [plans/2026-07-06-parakeet-v3-engine/](plans/2026-07-06-parakeet-v3-engine/):
  - `task.md` — problem statement, approach, workplan
  - `research.md` — model capabilities, runtime options (sherpa-onnx vs alternatives), fit with existing architecture, risk assessment
  - `implementation-plan.md` — 6-phase execution strategy (spike → Core → Platform → Desktop → Benchmark → tests/ADR/vault)
- [plans/INDEX.md](plans/INDEX.md) — added Planned row

## Decisions Made

1. **sherpa-onnx C# bindings** (in-process) over sidecar or hand-rolled ONNX Runtime
   - NuGet `org.k2fsa.sherpa.onnx` is official, pre-tested
   - Model files pre-converted in `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8`
   - Why not hand-rolled: ownership of mel-spec + TDT decode, correctness risk
   - Why not sidecar: ADR-024/025 pain (process lifecycle, cold start, port conflicts)

2. **CPU-only in v1** — acceptable because INT8 inference is the whole appeal
   - No pre-built sherpa-onnx CUDA NuGet exists (would need to compile from source)
   - Fast on CPU (many× real-time for short utterances)
   - Covers AMD/Intel users without GPU dependency

3. **No translation** — use the `TranslationForm.None` fallback already in `SpeechEngineCapabilities`
   - Parakeet is transcribe-only; the UI was written to support this shape (ADR-036)
   - Language page already renders a disabled target with explanatory note

4. **Single catalog entry (TdtV3Int8)** for now
   - Future: fp32, parakeet-v2 (English-only), faster variants
   - Mirrors `Gemma4ModelInfo` pattern, room to grow

5. **Source language always auto-detected** — cannot be forced
   - 25 European languages supported
   - Decide in Desktop phase whether to disable the source picker or show it greyed with a note (leaning: greyed + "auto-detected")

## Facts Learned

- **Parakeet TDT 0.6B v3 metrics** (research.md references)
  - ~6.34% avg WER on Open ASR Leaderboard (multilingual), beats Whisper large-v3
  - 600M params, FastConformer-TDT architecture, trained on ~670 k hours (NVIDIA Granary)
  - Native punctuation + capitalization (not CTC-only output)
  - Output includes optional word-level timestamps (not exposed by `TranscriptionResult` today)

- **sherpa-onnx C# API shape**
  - `OfflineRecognizer` wraps encoder/decoder/joiner ONNX files
  - Config: `OfflineRecognizerConfig` with `ModelType="nemo_transducer"`, `NumThreads`, `Provider="cpu"`
  - `OfflineStream` per transcription, `AcceptWaveform(16000, float[])`, synchronous `Decode`, result exposes `Text` + optional `Lang` field
  - Native dlls per RID (win-x64 included); all available from NuGet (no build-from-source req'd for CPU)

- **Model file distribution**
  - HF repo `csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3-int8` has individual files:
    - `encoder.int8.onnx` (652 MB)
    - `decoder.int8.onnx` (11.8 MB)
    - `joiner.int8.onnx` (6.4 MB)
    - `tokens.txt` (94 KB)
    - **Total ≈ 670 MB** (vs 6 GB Gemma, 1.5 GB Whisper-medium)
  - Download strategy: per-file HTTP (mimics Gemma4ModelDownloadService), cumulative progress bar

- **Codebase readiness**
  - `SpeechEngine` enum + factory + delegating recognizer = third engine is switch-case
  - `SpeechEngineCapabilities` has a fallback branch explicitly commented as what "Parakeet-style ASR" would declare
  - `Gemma4ModelInfo` / download service / settings VM = direct template for Parakeet
  - Audio pipeline 16 kHz mono float is Parakeet's native input — no resampling
  - ADR-038 (prewarm) and ADR-036 (language UX) already support this use case

## Open Blockers

- **Spike build validation** — must run Phase 0 (console spike) to confirm:
  - `org.k2fsa.sherpa.onnx` NuGet restores without warnings under `TreatWarningsAsErrors`
  - Transcription output quality and latency on test WAV
  - Whether `result.Lang` is populated for NeMo transducers

## Documentation Status

- **ADR**: Pending (ADR-041 to be written during Phase 6; triggers: new Core enum/record, new `PlatformServiceExtensions` registrations, new `.csproj` dependency, speech subsystem change)
- **Vault**: Pending (updates to `memory/services/*`, `memory/architecture/subsystems.md`, `memory/decisions/_index.md` during Phase 6)
- **Knowledge**: Pending (new note `memory/knowledge/sherpa-onnx.md` with NuGet quirks, native layout, spike findings)

## Next Action

**For the next session:** Start Phase 0 (spike build) in a throwaway console project:
1. Add `org.k2fsa.sherpa.onnx` NuGet reference
2. Download int8 model to a temp folder
3. Instantiate `OfflineRecognizer`, load encoder, and transcribe `test_wavs/*`
4. Verify output quality, check latency, confirm no warnings under `TreatWarningsAsErrors`
5. Document any C API surprises (encoding, error handling, threading)

If spike is clean, proceed with Phase 1 (Core contracts: `SpeechEngine.Parakeet`, `ParakeetModelInfo`, etc.).
