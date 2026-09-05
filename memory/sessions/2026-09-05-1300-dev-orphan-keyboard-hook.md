---
title: "Session: 2026-09-05 — Dev orphan holding the keyboard hook"
type: session
status: complete
tags: [desktop, startup, hotkeys, shutdown, p-invoke, adr-062]
created: 2026-09-05
summary: "A stopped `dotnet run` left Parlotype running headless with the SharpHook keyboard hook live. New dev-only ParentProcessExitWatcher binds a non-installed build's lifetime to its launcher (ADR-062)."
---

# Session: 2026-09-05 — Dev orphan holding the keyboard hook

## Active Focus

User report: Parlotype still dictates on the hotkey with no tray icon, no window, and
nothing called "Parlotype" in Task Manager — intermittently, during development.

Root cause: `dotnet run` runs the app as a child; when the `dotnet` host is stopped
abnormally the `WinExe` child is orphaned (no console ⇒ no `CTRL_CLOSE_EVENT`). The
SharpHook `SimpleGlobalHook` runs on a background thread — never keeps the process
alive, never blocks shutdown, but never stops either — so the orphan keeps injecting
text. `SingleInstanceGuard` then makes every later `dotnet run` defer to the zombie.
It shows as `dotnet.exe`; the tray icon is in the overflow.

Files:
- `src/Parlotype.Platform/Startup/NativeParentProcess.cs` (new) — `NtQueryInformationProcess` P/Invoke
- `src/Parlotype.Platform/Startup/InstalledBuild.cs` (new) — `VelopackLocator` install check
- `src/Parlotype.Desktop/Services/ParentProcessExitWatcher.cs` (new) — the watchdog
- `src/Parlotype.Desktop/App.axaml.cs` — arm the watcher; reorder `desktop.Exit` handler
- `src/Parlotype.Desktop/Parlotype.Desktop.csproj` — narrow `InternalsVisibleTo`
- Tests: `Parlotype.Tests/NativeParentProcessTests.cs`, `Parlotype.Desktop.Tests/ParentProcessExitWatcherTests.cs`
- `docs/decisions/062-dev-parent-process-watchdog.md` (new)

## Decisions Made

- **Dev watchdog only** (user chose this over a hard-exit safety net or full hardening).
  Non-installed builds watch their launcher and shut down when it exits; installed
  builds are excluded (`InstalledBuild.IsInstalled`, non-Windows, or unresolvable parent
  all no-op).
- **Graceful first, then force.** On launcher exit: post `desktop.Shutdown()`, wait 5 s,
  `Environment.Exit(0)` if still alive. `IDisposable`, disposed from `desktop.Exit` so a
  clean tray-Exit cancels the fallback.
- **Install state from `VelopackLocator`, not a new `IUpdateService` member.** Keeps the
  change out of Core; same test `WindowsRunKeyLaunchAtLoginService` already uses.
- **`desktop.Exit` hardening**: hotkey coordinator + watcher disposed before the first
  `await` in the `async void` handler.
- Verified **not** the cause: Avalonia 12.0.2 already handles `WM_TASKBARCREATED`.

## Facts Learned

Distilled to `memory/knowledge/dotnet-run-orphans-and-parent-pid.md`:

- SharpHook `BasicGlobalHookBase.RunAsync` → `Thread { IsBackground = runAsyncOnBackgroundThread }`
  (decompiled). Parlotype passes `true`, so the hook thread is genuinely background —
  the orphan is a *fully-alive* process, not a lingering thread.
- No managed parent-PID API. `ntdll!NtQueryInformationProcess` +
  `ProcessBasicInformation.InheritedFromUniqueProcessId` — records the creator, never
  updated, so verify liveness. Declare struct fields `nint` for x64 alignment.
- `DllImport` is fine in this repo (`SYSLIB1054` not an error); matches `Win32KeyboardLayoutService`.

## Open Blockers

- None. End-to-end verified: killed only the `dotnet run` host with PowerShell, the
  `Parlotype.exe` child exited on its own within ~1 s.  Unit + full suite green (1195 tests,
  0 warnings). Installed-build path is unit-tested only — a packaged smoke test (tray app
  survives its launching stub exiting) is noted in the ADR for the release checklist.

## Documentation Status

- ADR: done — `docs/decisions/062-dev-parent-process-watchdog.md`
- Vault: done — `memory/services/desktop.md`, `memory/services/platform.md`,
  `memory/architecture/subsystems.md` (Single Instance section), `memory/decisions/_index.md` row 062
- Knowledge: done — `memory/knowledge/dotnet-run-orphans-and-parent-pid.md` + index row

## Next Action

**Done this session:** rebased on `master` (ADR number moved 061 → **062**; PR #23's
`061-translation-paused-state` had taken 061), full suite green (1216 tests, 0 warnings),
**PR #24 opened** — https://github.com/mdemin729/parlotype/pull/24.

Follow-up when convenient: a packaged smoke test asserting an installed tray instance keeps
running after its Velopack stub exits (the one path `ParentProcessExitWatcher` covers that
is currently unit-tested only).
