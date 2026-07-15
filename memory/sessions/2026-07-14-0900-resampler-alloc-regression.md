---
title: "Session: 2026-07-14 — Recording-allocation regression: resampler read-request sizing"
type: session
status: complete
tags: [audio-pipeline, performance, allocations, naudio, regression]
created: 2026-07-14
summary: "User's live dotnet-counters run showed recording allocation UP (38–40 MB/s vs master's 30). A/B harness cleared the pipeline rework; a resampler probe found NAudio allocates per Read proportional to the requested count. Fixed by right-sizing the request: ~1.9 MB/s, 14× below master."
---

# Session: 2026-07-14 — Resampler allocation regression

## Active Focus
Follow-up to plan 2026-07-11-audio-pipeline-perf-security: the user ran the
pending live `dotnet-counters` verification and reported recording allocation
*increased* after the fix (38–40 MB/s vs ~30 MB/s on master; idle clean at
8–16 KB/s, same on Parakeet and OpenAI cloud).

## Decisions Made
- Root cause (two layers):
  1. Regression: `WasapiAudioCaptureService` passed the **rented array's
     `.Length`** to `ISampleProvider.Read` — ArrayPool bucket rounding turned
     the 38,400 request into 65,536, and NAudio's resampler chain allocates
     per call proportional to the *requested* count (4.68 MB/callback).
  2. Pre-existing elephant: master's `BytesRecorded`-sized request was itself
     the dominant recording allocator (2.73 MB/callback ≈ 27 MB/s) — not the
     capture buffer that ADR-045 originally targeted.
- Fix: request ~2× the expected resampled output
  (`inputFrames × 16000 / nativeRate`, min 1,024), pass that count (never
  `.Length`). Measured 0.19 MB/callback ≈ 1.9 MB/s — ~14× below master;
  identical sample delivery (probe asserts equal total samples out).
- ADR-045 amended; `ResamplerReadBenchmarks` added to MicroBenchmarks.

## Facts Learned
- [[naudio-resampler-read-cost]]: NAudio WDL resampler chain allocates per
  `Read` proportional to requested count regardless of returned count.
- Method note: the A/B harness (real `AudioPipelineService` + real Silero VAD,
  fake mic/recognizer, identical synthetic audio, master via detached
  `git worktree`) measured **268 KB per audio-second on both branches** —
  cleanly exonerating the channel/segmenter rework before probing NAudio.

## Open Blockers
- None. User should re-run `dotnet-counters` after this fix; expected
  recording allocation ≈ 4–8 MB/s (≈2 MB/s resampler + waveform UI rendering,
  which harnesses exclude). Other manual items from benchmarks-final.md
  (Win+V, URL hint, Benchmark smoke) still open.

## Documentation Status
- ADR-045 amendment; benchmarks-final.md addendum; knowledge entry + index;
  wasapi-capture-buffer-sizing corrected.

## Next Action
User re-verifies with `dotnet-counters monitor` during a real dictation;
remaining manual checks from benchmarks-final.md.
