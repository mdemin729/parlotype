---
title: "Session: 2026-06-18 — Keyboard-layout detection fix + log-spam reduction"
type: session
status: active
tags: [language, keyboard-layout, win32, pinvoke, platform, viewmodel, logging]
created: 2026-06-18
summary: "Fixed the 'System keyboard layout' source-language never updating when the layout was switched while focus was in another app. Root cause proven empirically: keyboard layouts are per-thread and modern multi-thread apps (Win11 Notepad) expose a frame thread (stale) distinct from the focused input thread (live); GetForegroundWindow→GetWindowThreadProcessId returns the frame thread. Fix: drill to the focus window's thread via GetGUIThreadInfo(fg).hwndFocus and read that thread's layout. Then reduced Win32KeyboardLayoutService log spam (~2/sec) by removing its logging and having LanguageRelationshipViewModel.RefreshKeyboardLayout log only on change. Branch fix_language_seettings_bugs; 348 Core/Platform + 214 Desktop tests green. Changes uncommitted."
---

# Session: 2026-06-18 — Keyboard-layout detection fix + log-spam reduction

## Active Focus

Branch `fix_language_seettings_bugs`. **Working tree uncommitted** (8 modified files).

1. **Live keyboard-layout detection across apps** (`Win32KeyboardLayoutService.cs`)
   — the "System keyboard layout" source-language option never reflected an
   Alt+Shift layout switch made while focus was in another application; it only
   updated after focus returned to Parlotype. Added `ResolveInputThread()`:
   `GetForegroundWindow()` → its thread → `GetGUIThreadInfo(fgThread).hwndFocus`
   → the focus control's thread → `GetKeyboardLayout(thatThread)`. Added the
   `GetGUIThreadInfo` P/Invoke + `GUITHREADINFO` struct.

2. **Live-poll wiring** (prior to this fix, kept) — `LanguageRelationshipViewModel`
   has a reference-counted ~500 ms `DispatcherTimer` (`BeginLivePolling` /
   `EndLivePolling`) attached/detached by `TranscribeWindow.axaml.cs` and
   `LanguageSelectionSettingsView.axaml.cs` so the displayed layout refreshes
   while either surface is visible.

3. **Log-spam reduction** — removed `ILogger` field/ctor param + per-poll
   `LogDebug` from `Win32KeyboardLayoutService` (now parameterless ctor; DI-only
   so safe). `LanguageRelationshipViewModel.RefreshKeyboardLayout()` now captures
   previous, returns early when unchanged (record value equality), and logs a
   single Debug line only when the layout actually changes.

## Decisions Made

- **Read the focus window's thread, not the foreground window's thread.** The
  textbook `AttachThreadInput` dance is unnecessary; `GetGUIThreadInfo` already
  exposes `hwndFocus` (the focused control across the input queue), and
  `GetKeyboardLayout` reads any thread's layout accurately. Classic single-thread
  apps are unaffected (focus window lives on the same thread).
- **Service no longer logs; callers do.** Per-poll detection ran ~2/sec; logging
  there was pure spam. The VM is the right place to log, and only on change.
- A **tracker-based** variant (consuming `ITargetWindowTracker` to pick the
  thread) was tried in an earlier session and **reverted** — it caused a "frozen
  `ru`" regression. The live poll + focus-thread drilling is the kept approach.
- Left `AudioPipelineService` / `LlamaCppSpeechRecognizer` `Detect()` callers
  unchanged — they call once per recording, not in a loop, so no spam.

## Facts Learned

- **Keyboard layouts are per-thread on Windows.** Modern packaged / WinUI / XAML-
  island apps (Win11 Notepad) spread their UI across many threads (Notepad showed
  **12**); the top-level frame window's thread is NOT the text-input thread, and
  its reported layout is stale. Proven with two throwaway probes:
  - `probe4` (deterministic, no foreground dependency): `GetKeyboardLayout` reads
    another thread's layout live/accurately when that thread owns the change
    (`plain=ru attached=ru`) → the API is **not** broken.
  - `probe5` (enumerates a process's threads/windows + per-thread layout):
    Notepad's `'Notepad'` frame thread reported stale `en` while the
    `'NotepadTextBox'` input thread held live `ru`.
- `GetForegroundWindow()` → `GetWindowThreadProcessId()` returns the **frame**
  thread; `GetGUIThreadInfo(fgThread).hwndFocus` bridges into the focused input
  control's thread even when a different thread owns it (e.g. frame F23C → focus
  on DB7C `RichEditD2DPT`).
- `KeyboardLayoutInfo` is a `record` (value equality), so `current == previous`
  is the correct change check in `RefreshKeyboardLayout`.
- Under `TreatWarningsAsErrors`, an assigned-but-unused private field is CS0414 —
  the `_logger` field/param had to be fully removed, not just the log line.

## Open Blockers

- **Unrelated NU1903** (warning-as-error) now breaks the full
  `dotnet build Parlotype.slnx`: transitive `SQLitePCLRaw.lib.e_sqlite3` 2.1.10 in
  the **Benchmark** project has a newly-published high-severity advisory. Out of
  scope for the language work; not addressed. Affected projects build clean when
  built directly. Decide separately whether to bump the package or suppress.
- Recurring Windows file-lock on `Parlotype.Platform.dll` from stray `.NET Host`
  processes — kill by PID then rebuild.

## Documentation Status

- ADR: **done** — amended `docs/decisions/036-language-ux-rebuild.md` with an
  "Amendment — live keyboard-layout detection" section (live poll + focus-thread
  drilling via `GetGUIThreadInfo`). New P/Invoke (`GetGUIThreadInfo`) + touches a
  language/settings surface, so the amendment satisfies the DoD trigger.
- Vault: **done** — `memory/services/platform.md` notes the focus-thread drilling;
  `memory/services/desktop.md` modified (prior fixes' symbols). Worth confirming
  `RefreshKeyboardLayout` change-only-logging + live-poll API are listed.
- Knowledge: **pending (recommended)** — the per-thread / multi-thread-app /
  `GetGUIThreadInfo(hwndFocus)` discovery is non-derivable from code and broadly
  reusable; consider distilling into `memory/knowledge/` (e.g.
  `win32-keyboard-layout.md`).

## Next Action

Commit the working tree (8 modified files) on `fix_language_seettings_bugs` with a
message covering both the cross-app keyboard-layout detection fix and the log-spam
reduction (no AI-attribution trailer per user preference). Then decide how to
handle the unrelated Benchmark NU1903 advisory (bump `SQLitePCLRaw` or scoped
`NoWarn`). Optionally distil the per-thread keyboard-layout knowledge into
`memory/knowledge/`.
