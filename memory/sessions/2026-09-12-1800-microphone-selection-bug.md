---
title: "Session: 2026-09-12"
type: session
status: active
tags: [audio-pipeline, microphone, bugfix]
created: 2026-09-12
summary: "Fixed AudioPipelineService always capturing from the OS default microphone, ignoring SelectedMicrophoneId"
---

# Session: 2026-09-12

## Active Focus
- [src/Parlotype.Platform/Audio/AudioPipelineService.cs](../../src/Parlotype.Platform/Audio/AudioPipelineService.cs) — `StartAsync`, `CacheSettingsAsync`
- [src/Parlotype.Tests/AudioPipelineTests.cs](../../src/Parlotype.Tests/AudioPipelineTests.cs)
- [src/Parlotype.Tests/AudioPipelineSingleUtteranceTests.cs](../../src/Parlotype.Tests/AudioPipelineSingleUtteranceTests.cs)

## Decisions Made
- Resolved `SettingsKeys.SelectedMicrophoneId` inside `AudioPipelineService.CacheSettingsAsync` (same place every other per-session setting is snapshotted) rather than in `StartAsync` directly, so the resolved `MicrophoneInfo?` is cached alongside `_whisperOptions`/`_silenceThresholdSamples` before capture starts.
- Added `IMicrophoneEnumerator` as a new constructor dependency on `AudioPipelineService` instead of introducing a separate resolver service — it was already DI-registered (`WasapiMicrophoneEnumerator`) and is exactly what `MicrophoneSettingsViewModel` already uses to enumerate/match devices.
- Fallback when the saved device ID doesn't resolve (unplugged, or unset) is `null`, which `WasapiAudioCaptureService.StartAsync` already treats as "use the OS default capture endpoint" — no new sentinel value needed.
- No ADR: this only wires an existing registered service into an existing constructor; none of the ADR triggers (new Core interface/enum, new `PlatformServiceExtensions` entry, new `.csproj` dependency, OS-conditional behaviour, new native/process integration) actually fire.

## Facts Learned
- Root cause of the reported bug ("selected AnkerWork mic, but Plantronics headset — the system default — was still used"): `MicrophoneSettingsViewModel` persists the user's choice to `SettingsKeys.SelectedMicrophoneId` correctly, and `WasapiAudioCaptureService.StartAsync(MicrophoneInfo? device, ...)` already fully supports starting on a specific device — but the only call site, `AudioPipelineService.StartAsync`, hardcoded `_capture.StartAsync(null, cancellationToken)` and never read the setting at all. A textbook "settings UI writes a key nothing downstream reads" gap, not a NAudio/WASAPI issue.
- `AudioPipelineService` has no test seam for asserting which device capture receives — `TestAudioCaptureService.StartAsync` in `AudioPipelineTests.cs` discarded the `device` parameter entirely. Added `LastRequestedDevice` so this class of regression is directly assertable.

## Open Blockers
- None.

## Documentation Status
- ADR: none required (see Decisions Made)
- Vault (services/architecture): done — [[services/_index|platform.md]] `AudioPipelineService` entry updated to mention the `IMicrophoneEnumerator` dependency and `SelectedMicrophoneId` resolution
- Knowledge (non-derivable facts): none — the fix is now plainly visible in code; no third-party quirk or environment gotcha to preserve separately

## Next Action
No open thread from this session. If the user later reports a *different* device still being ignored, check whether `WasapiMicrophoneEnumerator.GetAvailableMicrophones()` returns a stable `MMDevice.ID` across reboots/driver reinstalls — that ID equality is what the new resolution in `CacheSettingsAsync` depends on, and it was not independently verified here.
