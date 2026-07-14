---
title: Audio pipeline performance/allocation improvements + application security audit
status: in_progress
created: 2026-07-11
started: 2026-07-13
completed:
---

# Audio Pipeline Performance + Security Audit

## Problem

Two independent asks, planned together because they touch the same subsystems:

1. **Performance / allocations.** The audio pipeline (WASAPI capture → VAD →
   recognizer → text injection) allocates far more than it needs to on hot
   paths. The worst offender is the capture callback, which allocates a fresh
   oversized `float[]` (~77–154 KB, crossing the 85 KB LOH threshold for
   typical 48 kHz device formats) up to 20× per second while recording. Several
   full-utterance buffer copies are also avoidable, VAD inference runs on the
   audio capture thread (risking silent sample drops via
   `DiscardOnBufferOverflow`), and the transcription loop polls with
   `Task.Delay(50)`. Full inventory with file:line references:
   [research.md](research.md) §1.

2. **Security audit.** No systematic security review has been done. The audit
   performed while preparing this plan found 9 issues (2 high): transcribed
   text is persisted to plaintext rolling log files at the app's default
   `Debug` log level, and Whisper/Parakeet model downloads are never
   integrity-checked even though `WhisperModelInfo.Sha` metadata exists (the
   llama-server installer *does* verify SHA-256, so the precedent is in-repo).
   Full findings table: [research.md](research.md) §2.

## Goals

- Reduce steady-state managed allocation during recording by an order of
  magnitude on the capture/buffering path; eliminate recurring LOH allocations
  outside model weights.
- Prove improvements with BenchmarkDotNet before/after tables (new
  `Parlotype.MicroBenchmarks` project) plus a GC-counter check on the running
  app; guard against WER/RTF regressions with the existing `Parlotype.Benchmark`
  smoke config.
- Produce a written security audit report (`docs/security/`) covering all
  findings including accepted risks, and remediate the agreed subset.
- Zero behaviour change to transcription output: WAV encoding stays
  byte-identical, pipeline events/ordering/drain semantics unchanged.

## Non-Goals

- No changes to model inference itself (Whisper.net / sherpa-onnx / llama.cpp
  internals) — only how we hand data to them.
- No new cloud providers, no `ISecretStore` keychain work for Linux/macOS
  (already tracked as an ADR-043 deferred item; documented as accepted risk).
- No UI redesign; settings validation gets inline errors only where a security
  fix requires it (cloud base URL).

## Acceptance Criteria

1. `dotnet build Parlotype.slnx` clean (zero warnings), `dotnet test` green.
2. BenchmarkDotNet results committed to the plan folder showing allocation
   reductions for: WAV encoding, sample buffering, streaming window extraction.
3. Capture callback no longer allocates per callback (pooled buffer), verified
   by benchmark + code inspection; `dotnet-counters` GC stats during a 60 s
   dictation show materially lower alloc rate (recorded in results doc).
4. `Parlotype.Benchmark` smoke run: WER unchanged, RTF not regressed.
5. `docs/security/2026-07-11-security-audit.md` exists with every finding,
   severity, and disposition (fixed / accepted / deferred).
6. Agreed security fixes implemented with tests: no transcript text in logs,
   SHA-256 verification on Whisper + Parakeet downloads (fail closed),
   HTTPS-or-loopback enforcement for cloud base URLs, clipboard
   history/cloud-sync exclusion on injected text, atomic settings/secrets
   writes, `ArgumentList` for llama-server spawn.
7. ADRs for: the new benchmark project/dependency, the pipeline changes, and
   the security hardening batch. Memory vault + INDEX updated per Definition
   of Done.

## Open Questions (for review before implementation)

1. **Phase 2 scope** — the threading rework (VAD off the capture thread,
   `Channel<T>` instead of polling) is the only medium-risk change. Include it,
   or ship Phase 0–1 + security first and decide after measurements?
2. **Transcript logging policy** — remove transcript text from logs entirely
   (recommended), or keep it behind an explicit opt-in "verbose diagnostics"
   setting? Same question applies to logging cloud error response bodies.
3. **llama-server auth** — add a random per-session `--api-key` to the sidecar
   (cheap, closes local-process access), or document as accepted risk?
4. **Benchmark project shape** — `src/Parlotype.MicroBenchmarks` console app,
   in the solution but excluded from `dotnet test`, run manually in Release.
   OK, or prefer it under a `bench/` folder outside `src/`?

## Workplan

- [ ] Phase 0 — BenchmarkDotNet scaffolding + baseline capture ([implementation-plan.md](implementation-plan.md) §Phase 0)
- [ ] Phase 1 — Allocation fixes on hot paths (capture pool, WavEncoder, Parakeet copy, streaming copy, buffer pre-size)
- [ ] Phase 2 — Pipeline threading rework (Channel, VAD off capture thread) — *pending go/no-go*
- [ ] Phase 3 — Security audit report + agreed remediations
- [ ] Phase 4 — ADRs, memory vault, benchmark result docs, session note
