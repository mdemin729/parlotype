---
title: "Session: 2026-05-02 Silence Threshold Analysis & Cleanup"
type: session
status: active
tags: [audio, vad, benchmark, silence-detection]
created: 2026-05-02
summary: Analyzed silence threshold clamping, built PipelineSimulator for benchmarks, proved sub-500ms thresholds are catastrophic (77% WER), removed Instant/VeryShort/Short from WaitTimeOption
---

# Session: 2026-05-02 Silence Threshold Analysis

## Active Focus
- `src/Parlotype.Core/Speech/WaitTimeOption.cs` — removed Instant, VeryShort, Short enum values
- `src/Parlotype.Platform/Audio/AudioPipelineService.cs` — removed clamping logic
- `src/Parlotype.Benchmark/Pipeline/PipelineSimulator.cs` — **new** real-time pipeline simulator
- `src/Parlotype.Benchmark/Pipeline/BenchmarkRunner.cs` — pipeline simulation mode integration
- `src/Parlotype.Benchmark/Configuration/BenchmarkConfig.cs` — added `VadConfig.SilenceThresholdMs`
- `src/Parlotype.Benchmark/Configuration/SweepExpander.cs` — added `vad.silenceThresholdMs` axis
- `src/Parlotype.Desktop/ViewModels/Settings/SpeechSettingsViewModel.cs` — legacy settings migration
- `src/Parlotype.Tests/AudioPipelineTests.cs` — silence threshold behavior tests
- `src/Parlotype.Benchmark.Tests/PipelineSimulatorTests.cs` — **new** simulator tests

## Decisions Made
- **Removed sub-500ms WaitTimeOption values** — benchmark data (954 transcriptions) proved they cause 77-80% WER vs 19.7% at 500ms. The clamping was a safety mechanism, not a bug. (ADR-019)
- **PipelineSimulator design** — static class mirroring AudioPipelineService.ProcessBatch() but without clamping. Feeds audio in 10ms callbacks, runs VAD in 500ms chunks, flushes on silence threshold.
- **SilenceThresholdMs as nullable** — `null` preserves existing one-shot benchmark behavior; any value activates pipeline simulation. Backward compatible.
- **Legacy settings migration** — SpeechSettingsViewModel detects invalid enum values, rewrites to Medium, and syncs UI state (found via code review).
- **Final flush fidelity** — PipelineSimulator reruns VAD on entire remaining buffer at EOF, matching AudioPipelineService.FlushBuffer() (found via rubber-duck critique).

## Facts Learned
- Sub-500ms silence thresholds cause Whisper to receive audio fragments, producing hallucinated output (>100% WER possible on short samples)
- The benchmark infrastructure tests VAD segment splitting, NOT pipeline flush timing — these are different mechanisms. A `PipelineSimulator` was needed to test flush behavior.
- Silero VAD supports chunks as small as 64ms but accuracy degrades below ~250ms
- Russian accent sample with pauses is especially sensitive to threshold: 83% WER at 100ms → 33% at 500ms → 11% at 3000ms

## Open Blockers
- None

## Documentation Status
- ADR: done — `docs/decisions/019-remove-sub-500ms-silence-threshold.md`
- Vault (services/architecture): none required (no new services/subsystems)
- Knowledge (non-derivable facts): done — updated `memory/knowledge/vad-silence-threshold-constraint.md` with benchmark results and resolution

## Next Action
- Consider sourcing a challenging audio dataset (Common Voice, TED-LIUM, CHiME) for stress-testing transcription quality with different configurations
- The `PipelineSimulator` enables future experiments if sub-500ms support is revisited via adaptive VAD chunking
