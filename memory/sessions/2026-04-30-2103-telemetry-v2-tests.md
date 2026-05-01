---
title: "Session: 2026-04-30 — Telemetry Investigation + V2 Integration Tests"
type: session
status: active
tags: [avalonia12, telemetry, testing, desktop-v2, integration-tests]
created: 2026-04-30
summary: Investigated Avalonia 12 build-time telemetry (decompiled BuildServices, confirmed real outbound HTTP), then added V2 integration tests covering the record → transcribe → inject flow.
---

# Session: 2026-04-30 — Telemetry Investigation + V2 Integration Tests

## Active Focus
- `memory/knowledge/avalonia-devtools.md` — rewrote "Privacy notes" section with full decompilation findings
- `memory/knowledge/_index.md` — updated summary row for `avalonia-devtools`
- `src/Parlotype.Desktop.V2.Tests/Mocks/MockAudioPipeline.cs` — **new** mock implementing `IAudioPipeline`
- `src/Parlotype.Desktop.V2.Tests/Mocks/MockTextInjectionService.cs` — **new** mock implementing `ITextInjectionService`
- `src/Parlotype.Desktop.V2.Tests/TranscribeViewModelTests.cs` — expanded from 2 to 7 tests
- `src/Parlotype.Desktop.V2.Tests/HotkeyCoordinatorTests.cs` — added full-flow press→record→transcribe→inject→release test

## Decisions Made
- **Accept Avalonia build telemetry for now** — Community tier cannot opt out (`AVALONIA_TELEMETRY_OPTOUT` is ignored). Data is hashed/anonymised and build-only (no runtime telemetry). Will set `AVALONIA_TELEMETRY_OPTOUT=1` when upgrading to a paid tier.
- **Use `[AvaloniaFact]` for all V2 tests** — even those that don't directly touch `Dispatcher.UIThread`, for consistency. `HotkeyCoordinator` dispatches via `Dispatcher.UIThread.Post`, so the full-flow test requires it.
- **`Task.Delay(100)` for async void handler settling** — `TranscribeViewModel.OnTranscriptionAvailable` is `async void`, so tests need a small delay after raising events to let the handler complete.

## Facts Learned
- `Avalonia.BuildServices` 11.3.2 `AvaloniaStatsTask` writes binary telemetry to `%LOCALAPPDATA%/AvaloniaUI/BuildServices/` and spawns `Avalonia.BuildServices.Collector.dll` as a background process that HTTP-POSTs to `https://av-build-tel-api-v1.avaloniaui.net/api/usage`.
- Telemetry payload includes: SHA256-hashed project name, TFM, RID, Avalonia version, OS description, CPU architecture, IDE detection, CI provider detection, and `DeviceUniqueId` (SHA256 of `MachineName-UserName-OSPlatform`).
- `HasOptedOut()` checks for `AVALONIA_TELEMETRY_OPTOUT` env var but the opt-out path is only reachable for paid tiers (Indie/Business/Enterprise) — Community and Trial always run telemetry.
- No telemetry types exist in Avalonia runtime DLLs (`Avalonia*.dll` in Release output) — confirmed by scanning with ILSpy.
- Stored as permanent knowledge in `memory/knowledge/avalonia-devtools.md` and as agent memory.

## Open Blockers
- None.

## Documentation Status
- ADR: none required — telemetry is accepted as-is, no code change; tests are test-only
- Vault (services/architecture): none required — no new services or architecture changes
- Knowledge (non-derivable facts): done — `memory/knowledge/avalonia-devtools.md` updated with full telemetry investigation

## Next Action
Pick up from a clean slate. Suggested follow-ups from last session (remaining items):

1. **Decide V1 sunset path** — ADR 015 leaves V1 + V2 coexisting; eventually one needs to be retired with a migration ADR.
2. **Investigate suppressing the Avalonia build telemetry message** — the message itself (`Avalonia Accelerate Community requires telemetry...`) is noisy in build output; could suppress with MSBuild `NoWarn` or log-level filtering even if telemetry itself can't be blocked.

Commits this session: `42c22f9` (telemetry docs), `d1e30dc` (V2 integration tests). 20 V2 tests passing.
