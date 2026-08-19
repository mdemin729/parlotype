---
title: Hold-scoped push-to-talk (suppress mid-recording flush)
status: completed
created: 2026-08-18
started: 2026-08-18
completed: 2026-08-18
---

# Hold-scoped push-to-talk

## Problem

In push-to-talk the key release is an explicit end-of-utterance signal, but
`AudioPipelineService.ProcessBatch` still flushes and injects whenever trailing
silence exceeds the `WaitTime` setting. Users pausing mid-sentence get the
sentence cut, injected early, and punctuated as a fragment — then have to edit it
back together. The reporter is already on the longest available setting
(Very Long, 3 s) and still hits it, so no setting change can fix it.

## Findings

See [research.md](research.md). Summary:

- Suppressing the flush is confirmed equal-or-better up to ~60 s, and fixes the
  word corruption at cut boundaries ("this evening" → "V7").
- Parakeet loses 40–52% of words in a single decode past ~120 s, and **hard-crashes
  above 400 s** with a native SEH exception from ONNX Runtime.
- Whisper is unaffected at any length tested (600 s: 99% words kept, flat WER).
- The existing 30 s force-flush raw-dumps the buffer without VAD extraction — a
  latent quality bug of its own.

## Decisions taken

1. **Parakeet ceiling 60 s**, the last rung with full word retention — well clear of the
   400 s crash point, because quality collapses from ~120 s and a ceiling that only
   avoids the crash would let the app silently drop half the user's words.
2. **Whisper ceiling 300 s**, purely for latency; no quality knee was found at any length.
3. **Over the ceiling, split on a VAD boundary** rather than stopping or raw-dumping.
4. Mode is **derived from the gesture**, not a new setting.
5. Silence timeout copy reworded — it no longer claims to apply everywhere.

## Outcome

Shipped as [ADR-060](../../docs/decisions/060-hold-scoped-push-to-talk.md).

- `PipelineMode.SingleUtterance` + `SpeechEngineLimits` in Core
- `ProcessSingleUtterance` / `FlushAtSpeechBoundary` / `RetainFrom` in
  `AudioPipelineService`, with `RunIncrementalVad` extracted and shared with batch mode
- `HotkeyMatchResult.HoldScoped` → `DictationStartEventArgs` → `HotkeyCoordinator` →
  `TranscribeViewModel.StartRecordingAsync(bool)` → `StartAsync`
- Silence timeout settings copy corrected
- 19 new tests (11 pipeline/limits, 5 matcher, 3 desktop plumbing); full suite 1182 green

Benchmark-harness fixes made while measuring are described in [research.md](research.md);
the Whisper engine-selection bug is recorded in
`memory/knowledge/benchmark-engine-selection-trap.md`.

## Code review fixes

A review of the first implementation found four issues, all fixed:

1. **The ceiling measured elapsed hold time, not decode input.** `SpeechEngineLimits` was
   benchmarked against what the recognizer receives, but the check used the raw buffer
   length — so a hold of 10 s speech / 45 s pause / 10 s speech (65 s held, ~20 s decoded)
   split for no reason, reintroducing the mid-sentence cut this whole change removes, and
   worst for the pause-heavy users who reported it. Now measured via
   `AccumulatedSpeechSamples()`, with a separate 10-minute raw backstop for a hold that
   never ends (missed key-up).
2. **The single-segment split discarded the unscanned tail.** `ClearBufferState()` threw
   away everything past the last VAD segment, and VAD only runs per 8000 new samples — so
   up to ~500 ms of live speech was silently lost mid-word. Now cut at the ceiling with
   the overrun retained. A follow-up pass also found that a run which grew past the
   ceiling and *then* closed was flushed whole (a 200 s run going straight to Parakeet);
   the ceiling now binds in both cases.
3. **The engine could be read before the recognizer resolved a different one.**
   `SpeechRecognizerFactory` re-reads `SettingsKeys.SpeechEngine` independently. Narrow
   window, but the failure direction was bad (Parakeet running with a Whisper 300 s
   ceiling). The read moved after `InitializeAsync`, so any disagreement is now
   conservative.
4. **ADR-060 claimed the batch raw-dump was fixed when it wasn't** — only the new path
   had been. `ProcessBatch`'s overflow now extracts like every other path.

`RetainFrom` was generalized to rebase all surviving segments rather than assuming exactly
one. The pre-existing boundary-split test had encoded the buggy raw-buffer semantics and
was corrected. Six regression tests added (25 new tests total).

## Follow-ups not taken

- Gemma 4 and both cloud engines are unmeasured and take Parakeet's 60 s ceiling. Worth
  benchmarking Gemma 4 locally; the cloud ones cost money to measure.
- Neither cloud recognizer has a client-side request-size guard. Harmless at 60 s
  (OpenAI's 25 MB limit is ~13 min of 16 kHz mono WAV) but load-bearing if that ceiling
  ever rises.
- Whisper results in `results/` from between ADR-041 and the harness fix are suspect and
  would need re-running before being cited.
