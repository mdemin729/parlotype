---
title: What benchmark memory and runtime figures do not measure
type: knowledge
tags: [benchmark, memory, gemma, llamacpp, metrics]
created: 2026-09-07
last_updated: 2026-09-07
summary: PeakWorkingSet64 is a high-water mark that includes warm-up and excludes child processes, and EnvironmentInfo.WhisperRuntime reads a Whisper-only static so every non-Whisper engine reports "unknown"
---

# What benchmark memory and runtime figures do not measure

Three caveats about `Parlotype.Benchmark` output that the numbers themselves do
not advertise. They make certain cross-engine comparisons meaningless rather than
merely noisy.

## 1. Peak RAM is a process high-water mark, so warm-up is inside it

Per-sample memory comes from `Process.PeakWorkingSet64`, which never falls. Once
[[benchmark-warmup]]'s throwaway transcription has run, its allocation is baked
into the peak for **every** sample that follows. Per-sample RAM is therefore an
upper bound on the whole run to date, not a measurement of that sample.

## 2. Child-process memory is invisible

For llama.cpp/Gemma the model lives in the spawned `llama-server.exe`, which is a
**separate process** and contributes nothing to Parlotype's working set. The
engine with by far the largest real footprint reports the smallest. Comparing
Gemma's memory column against Whisper's or Parakeet's compares a sidecar's client
to an in-process model. Wiring external-process tracking was left as an open
follow-up.

## 3. `WhisperRuntime` is Whisper-only despite being a general field

`EnvironmentInfo.WhisperRuntime` is populated as:

```csharp
WhisperRuntime = RuntimeOptions.LoadedLibrary?.ToString() ?? "unknown",
```

That static belongs to Whisper.net and is only set once a `WhisperFactory` has
been built ([[vulkan-runtime-probing]] §3). Any llama.cpp, Parakeet or cloud run
therefore stores the literal `"unknown"` — which is then displayed as "Runtime"
in `ConsoleReporter`, persisted to SQLite, and compared as `RuntimeA`/`RuntimeB`
by `ResultComparer`. It is a missing value, not a detected one; the field name
outgrew its meaning when the second engine landed.

## Historical: Gemma Q8_0 instability

`gemma-4-E2B-it-Q8_0` intermittently emitted stray `<|channel>` tokens mid-
reasoning that crashed llama-server's chat-template parser with HTTP 500, forcing
a retry to obtain a clean 50-sample run; the reasoning bleed-through also pushed
RTF to 0.315 against a normal ~0.04. Observed 2026-05-23, never root-caused —
suspected sampler/prompt tuning. Recorded because a future Gemma benchmark
hitting HTTP 500s should suspect the quant before the harness.

Related: [[benchmark-engine-selection-trap]] (configs silently running the wrong
engine entirely), [[llamacpp-gemma4-integration]].
