---
title: "Session: 2026-07-01 PTT first-stop race fix"
type: session
status: complete
tags: [hotkey, push-to-talk, transcribe, race-condition]
created: 2026-07-01
summary: "Fixed the first-use Push-to-Talk bug where releasing the hotkey during the cold model load never stopped the recording."
---

# Session: 2026-07-01 — PTT first-stop race fix

## Active Focus
- Bug: first PTT press after app launch starts recording, but the key release does not stop it; a second press/release cycle is needed. Afterwards PTT works normally.
- Files: `src/Parlotype.Desktop/ViewModels/TranscribeViewModel.cs`, `src/Parlotype.Desktop.Tests/TranscribeViewModelTests.cs`, `docs/decisions/039-ptt-stop-waits-for-inflight-start.md`.

## Decisions Made
- Root cause: `StopRecordingAsync`'s `if (!IsRecording) return;` guard silently dropped a stop that arrived while the cold-start `StartRecordingAsync` was still awaiting the model load (async interleaving on the UI thread via `Dispatcher.UIThread.Post` in `HotkeyCoordinator`).
- Fix in the ViewModel (not `HotkeyCoordinator`) so all callers are covered: track the in-flight start as `Task? _startTask`; `StopRecordingAsync` awaits it before the `IsRecording` check; `StartRecordingAsync` is reentrancy-guarded. See ADR-039.
- Extracted `StartRecordingCoreAsync(IAudioPipeline pipeline)` — pipeline passed as a non-null parameter because nullable flow analysis doesn't cross the method boundary (build treats CS8602 as error).

## Facts Learned
- Warm model makes the race invisible: start completes within the key-hold time, which is why only the *first* PTT cycle misbehaved. ADR-038's opt-in prewarm masks but doesn't fix it.

## Open Blockers
- None. Accepted edge case (documented in ADR-039): press → release → press again during one model load ends with the queued stop winning.

## Documentation Status
- ADR: done — `docs/decisions/039-ptt-stop-waits-for-inflight-start.md`
- Vault (services/architecture): done — `memory/decisions/_index.md` row 039 (no new public symbols, so no service-profile change)
- Knowledge (non-derivable facts): none — the race is now documented in ADR-039 + code comments + regression tests

## Next Action
- Manually verify on a cold app start: press-and-release PTT during the model load and confirm the recording stops as soon as the load settles (automated regression tests already cover the logic: `StopRecording_DuringSlowStart_StillStops` and friends).
