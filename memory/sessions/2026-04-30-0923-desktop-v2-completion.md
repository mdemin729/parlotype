---
title: "Session: 2026-04-30 — Desktop V2 completion"
type: session
status: active
tags: [desktop-v2, avalonia12, xunit-v3, model-download]
created: 2026-04-30
summary: Finished Parlotype.Desktop.V2 — fixed xUnit ambiguity, fixed startup DI crash, committed.
---

# Session: 2026-04-30 — Desktop V2 completion

## Active Focus
- `src/Parlotype.Desktop.V2.Tests/Parlotype.Desktop.V2.Tests.csproj` — migrated from xunit 2.9.3 to xunit.v3 3.2.2 + xunit.runner.visualstudio 3.1.5
- `src/Parlotype.Desktop.V2.Tests/HotkeyCoordinatorTests.cs` — converted to `[AvaloniaFact]` (Dispatcher.UIThread access)
- `src/Parlotype.Desktop.V2.Tests/{HotkeyCoordinatorTests,MicrophoneSettingsViewModelTests}.cs` — threaded `TestContext.Current.CancellationToken` to satisfy xUnit1051
- `src/Parlotype.Desktop.V2/Services/SilentModelDownloadService.cs` — new `IModelDownloadService` impl (silent, no UI)
- `src/Parlotype.Desktop.V2/App.axaml.cs` — registered `SilentModelDownloadService`
- `docs/decisions/015-parlotype-desktop-v2-avalonia12.md` — new ADR
- `memory/services/desktop-v2.md` — new service profile
- `memory/services/_index.md`, `memory/decisions/_index.md` — updated indexes
- `README.md` — added V2 section

## Decisions Made
- **xUnit v3 in V2 tests** — `Avalonia.Headless.XUnit 12.x` transitively requires the v3 extensibility core, so mixing with xunit v2 caused CS0433 `FactAttribute` ambiguity. Migrated the V2 test project to xunit.v3; V1 tests remain on xunit v2.
- **`SilentModelDownloadService` in V2** — V2 is tray-first with no always-visible main window, so the V1 modal `ModelDownloadDialogService` is inappropriate. New silent variant just wraps `HttpModelDownloadService` and logs progress.
- **`Parlotype.Platform` does NOT register `IModelDownloadService`** — confirmed each frontend (Desktop V1, Desktop V2, Benchmark) registers its own. Documented in ADR 015 to prevent future "missing service" surprises.
- **Per-frontend log file naming** — V2 uses `parlotype-v2-{date}_{seq}.log` to coexist with V1's `parlotype-{date}_{seq}.log`.

## Facts Learned
- `Avalonia.Headless.XUnit 12.0.2` requires xunit.v3, not xunit v2 — already stored as a memory.
- xUnit v3 analyzer rule **xUnit1051** flags every async method that takes a `CancellationToken` without using `TestContext.Current.CancellationToken`. With `TreatWarningsAsErrors=true` this breaks the build; thread the token through `Task.Delay`, `StartAsync`, etc.
- Tests that touch `Avalonia.Threading.Dispatcher.UIThread` hang under plain `[Fact]` because no Avalonia app is initialized. Use `[AvaloniaFact]` from `Avalonia.Headless.XUnit` even though the test isn't visually rendering anything.
- xUnit v3 in-process runner: `dotnet test` works, but you can also invoke `<TestProject>.exe -class Foo.Bar -parallel none` directly for fast targeted runs without the SDK orchestration overhead.
- `WhisperSpeechRecognizer` (in `Parlotype.Platform`) constructor-injects `IModelDownloadService`, but `AddPlatformServices()` does NOT register that interface. Each frontend must register its own implementation, or DI throws at startup.

## Open Blockers
- None.

## Documentation Status
- ADR: done — `docs/decisions/015-parlotype-desktop-v2-avalonia12.md`
- Vault (services/architecture): done — `memory/services/desktop-v2.md` + index updates in `memory/services/_index.md` and `memory/decisions/_index.md`
- Knowledge (non-derivable facts): done — two repo memories upserted (Avalonia 12 OnLostFocus signature; Avalonia.Headless.XUnit 12 needs xunit v3). The "each frontend supplies its own `IModelDownloadService`" rule is now documented in ADR 015 itself, where it's discoverable from code review.

## Next Action
Pick up from a clean slate. Suggested follow-ups when V2 work resumes:
1. **Wire Whisper transcription into `TranscribeViewModel.TogglePlay`** — currently the Play button starts/stops recording but the audio→text pipeline is not yet hooked up in V2 (V1's `MainWindowViewModel` is the reference).
2. **Add an Avalonia 12 visual tree inspector alternative** — `Avalonia.Diagnostics` has no 12.x release; consider F12 dev tools when 12.x ships, or a temporary downgrade for dev-only diagnostics.
3. **Decide V1 sunset path** — ADR 015 leaves V1+V2 coexisting; eventually one needs to be retired with a migration ADR.

Commit `3bca8d4` on `main` is the current state.
