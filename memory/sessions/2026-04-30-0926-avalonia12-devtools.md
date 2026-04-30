---
title: "Session: 2026-04-30 — Avalonia 12 DevTools (V2)"
type: session
status: active
tags: [desktop-v2, avalonia12, devtools, diagnostics, telemetry]
created: 2026-04-30
summary: Wired DEBUG-only AvaloniaUI.DiagnosticsSupport + avdt global tool into Parlotype.Desktop.V2 to replace the retired classic Avalonia.Diagnostics F12 inspector.
---

# Session: 2026-04-30 — Avalonia 12 DevTools (V2)

## Active Focus
- `src/Parlotype.Desktop.V2/Parlotype.Desktop.V2.csproj` — added `AvaloniaUI.DiagnosticsSupport` 2.2.1 in a `Configuration == Debug` ItemGroup
- `src/Parlotype.Desktop.V2/App.axaml.cs` — `this.AttachDeveloperTools()` under `#if DEBUG` in `Initialize()`
- `docs/decisions/016-avalonia12-developer-tools.md` — new ADR
- `README.md` — V2 "Visual inspector" subsection (install steps, F12 gesture, portal signup link)
- `memory/services/desktop-v2.md` — Diagnostics section + `last_updated` correction (2026-05-12 → 2026-04-30)
- `memory/knowledge/avalonia-devtools.md` — new knowledge note
- `memory/decisions/_index.md`, `memory/knowledge/_index.md` — index rows added

## Decisions Made
- **DEBUG-only conditional `<PackageReference>`** — keeps `AvaloniaUI.DiagnosticsSupport` and its transitive `Microsoft.IO.RecyclableMemoryStream` out of Release builds. Verified empirically (zero matching DLLs in `bin/Release/net10.0`).
- **Per-developer `avdt` global tool** — installed via `dotnet tool install --global AvaloniaUI.DeveloperTools`, requires a free AvaloniaUI Portal account for first-time activation. Documented in README rather than enforced in code.
- **Scope: V2 only** — V1 (`Parlotype.Desktop`) stays on Avalonia 11 + classic `Avalonia.Diagnostics` until V1 is retired (separate decision).
- **Did not gate the in-app call by anything beyond `#if DEBUG`** — headless V2 tests inherit the DEBUG configuration, but `AttachDeveloperTools()` is benign when no `avdt` process is listening; all 218 tests pass.

## Facts Learned
- `AttachDeveloperTools()` is exposed as an extension on `Application` and resolves through the existing `using Avalonia;` directive — no extra `using` was needed in `App.axaml.cs`.
- The `AvaloniaUI.DiagnosticsSupport` package metadata advertises a separate `AvaloniaUI.DiagnosticsSupport.Avalonia.dll` for `netstandard2.0` (older fallback path) and `net8.0` / `net10.0` builds for modern targets.
- The base Avalonia 12 SDK targets emit a build-time message: `"Avalonia Accelerate Community requires telemetry. To opt out, please upgrade to a paid tier."` — independently of whether `AvaloniaUI.DiagnosticsSupport` is referenced. Confirmed by removing the package and doing a clean Release build of V2; V1 (Avalonia 11) does **not** emit this message. This is a privacy-relevant property of Avalonia 12 itself, distinct from DevTools wiring.
- The community port advertised online as `ClassicDiagnostics.Avalonia` is not actually published on NuGet.org as of 2026-04 — `dotnet package search` returns no results, so it can't be relied on as an alternative.
- The V2 audio→text pipeline is **already** wired end-to-end (`TranscribeViewModel` injects `IAudioPipeline` + `ITextInjectionService`, `HotkeyCoordinator` drives start/stop). The previous handoff's "Next Action #1" was stale and is now resolved without further work.

## Open Blockers
- None for the DevTools work itself.
- Pre-existing build telemetry message from Avalonia 12 SDK is documented but not addressed; surfacing it for a privacy-first project may be worth a follow-up investigation.

## Documentation Status
- ADR: done — `docs/decisions/016-avalonia12-developer-tools.md`
- Vault (services/architecture): done — `memory/services/desktop-v2.md` Diagnostics section + `memory/decisions/_index.md` row 016 + `memory/knowledge/_index.md` row
- Knowledge (non-derivable facts): done — `memory/knowledge/avalonia-devtools.md` covers the package split, licensing tiers, portal signup, DEBUG-only wiring pattern, and the SDK-level telemetry message

## Next Action
Pick up from a clean slate. Suggested follow-ups:

1. **Investigate the Avalonia 12 SDK telemetry message** — determine whether it represents actual outbound telemetry at runtime (not just a build-time print), and decide whether to suppress / opt-out / accept it. Privacy-first project; this is non-trivial branding-wise.
2. **Decide V1 sunset path** — ADR 015 leaves V1 + V2 coexisting; eventually one needs to be retired with a migration ADR.
3. **Add an integration test for the V2 record → transcribe → inject flow** — currently only defensive no-pipeline paths are covered (`TranscribeViewModelTests`).

Commit `41f4e84` on `master` is the current state.
