---
title: "Session: 2026-07-05/06 — Frameless compact TranscribeWindow (C2) + review fixes"
type: session
status: complete
tags: [transcribe-window, avalonia, frameless, ux, adr-040, settings, code-review]
created: 2026-07-05
summary: "Designed (4 HTML prototypes) and implemented the frameless 172x118 TranscribeWindow (ADR-040); ran an 8-angle code review; fixed the settings-file-mixing finding by splitting window-position persistence into a separate IWindowStateService/window-state.json"
---

# Session: 2026-07-05/06 — Frameless compact TranscribeWindow (C2) + review fixes

## Active Focus

- Plan `plans/2026-07-05-transcribe-window-compact-redesign/` — requirements + 4 HTML prototypes (A bar / B dock / C card / **C2 card+grip**, chosen by user)
- `src/Parlotype.Desktop/Views/TranscribeWindow.axaml(.cs)` — full C2 relayout, then reworked to use `IWindowStateService`
- `src/Parlotype.Desktop/Services/WindowManager.cs`
- New Core: `IWindowStateService`, `WindowPosition`, `WindowStateKeys` (`src/Parlotype.Core/Settings/`)
- New Platform: `JsonFileStore` (shared base extracted from `JsonSettingsService`), `JsonWindowStateService` (`src/Parlotype.Platform/Settings/`)
- Tests: `src/Parlotype.Desktop.Tests/TranscribeWindowChromeTests.cs`, `MockWindowStateService`, new `src/Parlotype.Tests/Settings/JsonFileStoreTests.cs`
- `docs/decisions/040-frameless-compact-transcribe-window.md`

## Decisions Made

- **ADR-040**: frameless 172×118 widget; drag confined to a top grip strip (user explicitly reversed an earlier drag-anywhere idea); ✕/Esc hide to tray; status text lives only in the root-chrome tooltip.
- Position save points: after `BeginMoveDrag` returns (Windows modal move loop ends at drop), on hide (✕/Esc), and on `Closing` — no debounced `PositionChanged` writes.
- **Ran an 8-angle code review** (correctness ×3, cleanup/reuse/simplification/efficiency ×3, altitude, conventions) via parallel Explore agents, then verified the strongest candidates directly (reading `JsonSettingsService`, `HotkeyCoordinator`, `LanguagePickerView`, and an empirical headless test) rather than spawning a verifier agent per candidate. 6 findings reported, ranked by confidence:
  1. `WindowManager.ShowTranscribe`'s async lambda passed to `Dispatcher.UIThread.Post` (which takes `Action`) is effectively async-void — unobserved exceptions (CONFIRMED)
  2. `SavePositionAsync`'s 4 call sites never catch/log settings-write failures, unlike the rest of the codebase's fire-and-forget convention (CONFIRMED)
  3. Escape hides the whole widget even when the language flyout is open in Toggle/None form, which has no child Escape handler (only Full-form's `LanguagePickerView` claims it) (PLAUSIBLE)
  4. `IsOnAnyScreen` sizes the window with the window's *current* `DesktopScaling`, not the target monitor's, on mixed-DPI setups (PLAUSIBLE)
  5. **X/Y position stored as two separate settings keys, mixed into the main settings.json — doubled lock/I/O, and transient window state mixed with long-lived settings** (CONFIRMED) → **fixed this session**, see below
  6. Position-persistence logic hand-rolled per-window rather than a reusable service (PLAUSIBLE/altitude, not fixed — out of scope, only one window needs it today)
- **Fix for #5**: introduced `IWindowStateService` (Core) — same `GetAsync<T>`/`SetAsync<T>` shape as `ISettingsService` but backed by its own file, `window-state.json`, never `settings.json`. Extracted a shared `JsonFileStore` abstract base (instance-level `SemaphoreSlim` + path, not the old static lock) so `JsonSettingsService` and the new `JsonWindowStateService` share load/save logic without sharing a file or lock. Collapsed the two-key `PosX`/`PosY` design into one `WindowPosition(X,Y)` record struct under one key, `WindowStateKeys.TranscribeWindowPosition` — also fixes the "doubled round-trip" half of the complaint.
- Findings #1/#2 (exception handling) and #3/#4/#6 were reported but **not fixed** this session — user only asked for #5. They remain valid follow-up candidates.

## Facts Learned

- Avalonia 12: `SystemDecorations` obsolete → `WindowDecorations`; `BeginMoveDrag` blocks until drop on Windows. Distilled to `memory/knowledge/avalonia12-frameless-window.md`.
- **Correction of this session's own earlier assumption**: headless `Screens.All` is *not* empty — verified empirically it reports one real virtual screen `(0,0,1920,1280)` @ scale 1.0, available even before `Show()`. The "no screen info, trust blindly" fallback in `IsOnAnyScreen` is real defensive code but is not what makes the headless tests pass — the intersection check runs for real. Also confirmed `Width`/`Height` set via AXAML are valid immediately (not layout-computed), so no pre-`Show()` measurement concern either.
- Killing a stray `.NET Host`/`Parlotype.Desktop` process by PID (per CLAUDE.md note) was needed twice this session to unblock `dotnet build`/`dotnet test` after file-lock errors — expected friction on this machine, not a code bug.

## Open Blockers

- None. Manual Windows pass still recommended for grip-drag feel and cross-restart position restore (headless can't exercise `BeginMoveDrag`).
- Findings #1, #2, #3, #4, #6 from the code review remain open if the user wants them addressed later.

## Documentation Status

- ADR: done — `docs/decisions/040-frameless-compact-transcribe-window.md` (amended for the storage split + 172×118 correction)
- Vault (services/architecture): done — `memory/services/{core,platform,desktop}.md`, `memory/decisions/_index.md` row 040
- Knowledge (non-derivable facts): done — `memory/knowledge/avalonia12-frameless-window.md` corrected + index row

## Next Action

If continuing this feature: address code-review findings #1/#2 (wrap `RestorePositionAsync`/`SavePositionAsync` failures in try/catch + `ILogger`, matching the `PrewarmAsync` convention elsewhere) and #3 (give the Toggle/None flyout forms their own Escape handling so it doesn't leak to the window). Otherwise, do the manual Windows verification pass noted above before considering ADR-040 fully closed out.
