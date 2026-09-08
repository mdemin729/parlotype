---
title: "Session: 2026-07-13 — Audio pipeline perf rework + security audit"
type: session
status: complete
tags: [audio-pipeline, performance, allocations, security, benchmarkdotnet]
created: 2026-07-13
summary: "Executed plan 2026-07-11-audio-pipeline-perf-security end-to-end: BenchmarkDotNet project + baselines, allocation fixes (pooled capture buffers, WavEncoder rewrite, zero-copy Parakeet), channel-staged pipeline threading, full security audit with S1–S7 remediations; ADR-044/045/046."
---

# Session: 2026-07-13 — Audio pipeline perf rework + security audit

## Active Focus
Two-part request (planned 2026-07-11, user approved with "go ahead"):
performance/allocation improvements to the audio pipeline with BenchmarkDotNet
evidence, and an application security audit with remediations.

## Decisions Made
- **ADR-044**: new `Parlotype.MicroBenchmarks` (BenchmarkDotNet 0.14,
  MemoryDiagnoser/ShortRunJob, not in `dotnet test`; frozen legacy copies of
  rewritten code keep before/after in one run; artifacts git-ignored, curated
  tables in the plan folder).
- **ADR-045**: capture callback rents from `ArrayPool<float>`
  (`AudioDataEventArgs.Buffer` valid only during the event — new Core
  contract); WavEncoder exact-size rewrite (byte-identical, frozen-legacy
  equivalence tests); Parakeet zero-copy; buffer pre-size + span `AddRange`
  (must ship as a pair — unsized `AddRange` allocates MORE than per-sample
  `Add`); pipeline = 3 channel-joined single-threaded stages (capture copy →
  segmenter/VAD → transcription), stop = channel-completion drain, VAD off
  the capture thread.
- **ADR-046**: transcripts never logged (standing convention; file sink
  capped at Information via `AddFilter<ZLoggerRollingFileLoggerProvider>`);
  SHA-256 verification on all model downloads (fail-closed mismatch ⇒ new
  `ModelIntegrityException`; fail-open + warn on missing digest);
  `CloudBaseUrlValidator` HTTPS-or-loopback; clipboard exclusion formats on
  injected text; `ArgumentList` for llama-server; `AtomicFileWriter` for
  settings/secrets. S5 (sidecar `--api-key`) deferred with rationale —
  breaks crash-orphan adoption + external servers, same-user threat already
  crosses DPAPI.
- `WhisperModelInfo.Sha` (SHA-1, never consumed anywhere) renamed to
  `Sha256` with values from the HF LFS API for the exact repo/revision the
  downloader uses; Parakeet/Gemma catalogs gained digests the same way.

## Facts Learned
- NAudio `BytesRecorded` is bytes of the *native* format → float-sizing from
  it over-allocated ~24× into the LOH (~1.5 MB/s while recording) —
  [[wasapi-capture-buffer-sizing]].
- HF tree API `lfs.oid` = authoritative SHA-256; non-LFS blobs (tokens.txt)
  must be downloaded and hashed — [[huggingface-lfs-digests]].
- Windows clipboard exclusion formats + same-session rule —
  [[windows-clipboard-exclusion-formats]].
- BDN measured: unsized `List<float>.AddRange(span)` produced *more* garbage
  (6.5 MB) than the per-sample Add loop (4.2 MB); only pre-size+AddRange wins
  (1.92 MB) — recorded in ADR-045 and benchmarks-baseline.md.

## Open Blockers
- None blocking. Manual verification pending (headless session):
  1) live dictation + `dotnet-counters` GC numbers, 2) Win+V shows no
  injected text, 3) inline base-URL error hint renders, 4) `Parlotype.Benchmark`
  smoke run for WER/RTF. Listed in
  `plans/2026-07-11-audio-pipeline-perf-security/benchmarks-final.md`.

## Documentation Status
- ADRs: 044, 045, 046 written and indexed.
- Vault: `services/core.md`, `services/platform.md`, `services/_index.md`
  (+ new `services/microbenchmarks.md`), `architecture/audio-pipeline.md`
  (threading section rewritten), `decisions/_index.md`, 3 knowledge entries.
- Audit report: `docs/security/2026-07-11-security-audit.md`.
- Plan completed + INDEX.md moved to Completed.

## Final State
- Commits on `claude/audio-pipeline-analysis-237b47`: `1ff6d9b` (Phase 0
  benchmarks + plan docs) → `99d89a7` (Phase 1 allocation fixes) →
  `38e9f3a` (Phase 2 channel threading) → `7a2a3a0` (Phase 3 security) →
  docs/vault closeout commit.
- Tests at close: 870 passed / 0 failed (463 + 297 + 110); zero-error build
  (3 pre-existing AVLN5001 warnings on full rebuild, unchanged).

## Next Action
Run the four manual verification items above on a live desktop session, then
consider the ADR-043 deferred list (key-validation ping, Linux/macOS keychain)
if cloud-provider work continues.
