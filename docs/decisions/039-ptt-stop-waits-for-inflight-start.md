---
status: accepted
date: 2026-07-01
---

# 039. Push-to-Talk stop waits for the in-flight recording start

## Context

In Push-to-Talk mode the very first hotkey press after app launch starts
recording, but releasing the key did not stop it. The recording stayed on
until the user pressed the hotkey a second time; from then on PTT behaved
normally.

Root cause: a race between recording start and stop in
`TranscribeViewModel`. On a cold start, `StartRecordingAsync` awaits
`IAudioPipeline.StartAsync`, which loads the speech model — several
seconds. The user releases the PTT key during that load, and
`HotkeyCoordinator` posts `StopRecordingAsync` to the UI thread. Async
interleaving on the UI thread lets the stop run while the start is still
awaiting, so its `if (!IsRecording) return;` guard silently drops the
stop. The start then completes, sets `IsRecording = true`, and the
recording runs unbounded. Once the model is warm, start completes within
the key hold time and the race never re-fires — matching the
"only the first time" symptom. ADR-038's opt-in prewarm masks the bug
but does not fix it (prewarm is best-effort and off by default).

## Decision

Make stop requests wait for an in-flight start instead of being dropped:

- `TranscribeViewModel` tracks the in-flight start as a `Task? _startTask`
  field (UI-thread only, no locking needed).
- `StartRecordingAsync` is reentrancy-guarded: a second call while a start
  is in flight is a no-op, so the pipeline is never started twice.
- `StopRecordingAsync` awaits `_startTask` (if any) before checking
  `IsRecording`. A release that arrives mid-load now stops the recording
  as soon as the start settles. If the start failed, `IsRecording` stays
  false and the stop remains a no-op.

The fix lives in the ViewModel rather than `HotkeyCoordinator` so every
caller is protected — the hotkey path, the Transcribe window's toggle
button, and the language `RelationshipChanged` auto-stop.

## Consequences

- First-use PTT behaves correctly: releasing the key during the initial
  model load stops the recording right after the load finishes (a short,
  possibly empty recording — the correct PTT semantic).
- Start/stop are effectively serialized per ViewModel; no caller can
  observe a stop being silently dropped mid-start.
- Edge case accepted: press → release → press again *during one model
  load* ends with the queued stop winning (recording stops once the load
  finishes even though the key is held). Rare in practice and resolved by
  the next press.
- Regression coverage in `TranscribeViewModelTests`:
  `StopRecording_DuringSlowStart_StillStops`,
  `StopRecording_DuringFailedStart_DoesNotStopPipeline`,
  `StartRecording_Reentrant_StartsPipelineOnce`.
