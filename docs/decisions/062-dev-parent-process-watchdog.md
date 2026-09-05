---
status: accepted
date: 2026-09-05
---

# 062. Development Builds Exit With Their Launcher

## Context

A user reported Parlotype still reacting to the dictation hotkey — hold **Ctrl**, speak,
text is injected — while there was **no tray icon and no window**, and no "Parlotype" in
Task Manager. It happened intermittently *during development*.

The process was a live, fully-functional orphan:

- Parlotype is tray-first with `ShutdownMode.OnExplicitShutdown` (ADR-040): it shuts down
  **only** when the tray **Exit** item runs `desktop.Shutdown()`. Closing the window hides
  it; there is no other exit path.
- The global keyboard hook (`SharpHookHotkeyService` → `SimpleGlobalHook`) runs with
  `runAsyncOnBackgroundThread: true`. A background thread never keeps the process alive and
  never blocks a shutdown — but it also does not *stop* until the process ends or the hook
  is explicitly disposed.
- Development runs the app as `dotnet run --project src/Parlotype.Desktop`. The real app is
  a **child** of the `dotnet` CLI host. When that host is stopped abnormally — closing or
  killing the integrated terminal, an IDE "stop", a `dotnet watch` restart race — the child
  is frequently **not** killed and receives no console-close signal (it is a `WinExe`, so
  it owns no console window to close). It keeps running: dispatcher pumping, audio pipeline
  armed, `HotkeyCoordinator` still injecting text. The window is hidden; the tray icon is
  in the notification-area overflow, or was dropped. In Task Manager it is **`dotnet.exe`**,
  not "Parlotype" — which is why it looks like nothing is there.
- `SingleInstanceGuard` (ADR-055) then makes it worse: every later `dotnet run` finds the
  orphan, calls `SignalPrimary()`, and exits — so a fresh build silently defers to the
  zombie and the developer never notices.

Verified *not* the cause: Avalonia 12.0.2's `Win32.TrayIconImpl` already handles the
`WM_TASKBARCREATED` broadcast (with `ChangeWindowMessageFilterEx`), so an Explorer restart
re-adds the icon.

The installed app does not have this problem: it is launched by Explorer or the Velopack
stub, gets a clean tray-Exit or session-end shutdown, and its `Update.exe` / auto-start
machinery is designed around that.

## Decision

**Non-installed builds bind their lifetime to the process that launched them.** New
`ParentProcessExitWatcher` (Desktop, DI singleton), armed from
`App.OnFrameworkInitializationCompleted` right after the hotkey coordinator:

- **No-op unless this is a dev build.** `InstalledBuild.IsInstalled` (Platform) —
  `VelopackLocator.Current.CurrentlyInstalledVersion is not null && !IsPortable`, the same
  test `WindowsRunKeyLaunchAtLoginService` uses (ADR-059) — must be false. Also a no-op on
  non-Windows and whenever the launcher cannot be resolved to a *live* process (a detection
  miss must never take the app down).
- **Resolve the launcher** via `NativeParentProcess.TryGetParentProcessId` — there is no
  managed parent-PID API, so `ntdll!NtQueryInformationProcess` with
  `ProcessBasicInformation` reads `InheritedFromUniqueProcessId` — then `Process.GetProcessById`
  and hold the handle (immune to PID reuse).
- **On launcher exit**: request the normal graceful shutdown (post `desktop.Shutdown()` to
  the UI thread, so the hook and the speech recognizer are disposed the same way tray-Exit
  does), then wait 5 s; if the process is still alive, `Environment.Exit(0)`.
- `IDisposable`, disposed from the `desktop.Exit` handler so a clean tray-Exit cancels the
  wait and never trips the fallback.

**Shutdown-handler hardening** (`App.axaml.cs`): `desktop.Exit` is an `async void` handler.
`_hotkeyCoordinator?.Dispose()` and `_parentProcessExitWatcher?.Dispose()` now run **before**
the first `await`, so a continuation that never resumes cannot leave the hook installed.

## Consequences

- A stopped `dotnet run` / IDE session no longer leaves an orphan holding the keyboard
  hook. `taskkill` on the `dotnet` host alone is enough; the child follows within ~1 s.
- `NtQueryInformationProcess` is an undocumented-but-stable NT API and a **new P/Invoke**
  (`ntdll.dll`). `InheritedFromUniqueProcessId` records the *creator* and is never updated,
  so a resolved id can be stale or (rarely) reused — accepted: the watcher only ever acts
  on a process it opened at startup and verified alive, and the cost of a false negative is
  just the pre-existing orphan behaviour.
- One new Core-adjacent seam avoided: install state is read from `VelopackLocator`, not a
  new `IUpdateService` member.
- `Parlotype.Desktop` gains `InternalsVisibleTo("Parlotype.Desktop.Tests")` — narrowly, for
  the watcher's test seam. The app's surface is otherwise still closed to the test project.
- Installed builds are entirely unaffected: `Start()` returns immediately. A packaged smoke
  test (tray app keeps running after its launching stub exits) is the end-to-end proof and
  is noted for the release checklist.
- The single-instance "a dev build defers to a running instance" behaviour (ADR-055) is
  unchanged — but the instance it defers to is now far less likely to be an invisible
  zombie.

## Alternatives rejected

- **Watch console/stdin for EOF.** A `WinExe` under `dotnet run` has no reliable console;
  stdin may be `NUL` (immediate false EOF) or an inherited console (no EOF on parent
  death). Fragile in exactly the cases that matter.
- **`Environment.Exit` at the end of `Program.Main`.** Only helps when a shutdown was
  actually initiated; the orphan never gets that far.
- **Let a new dev run take over an unresponsive primary.** Real fix for the *symptom* the
  developer sees, but it means defining "unresponsive", racing two instances over the hook
  and settings file, and shipping that risk to the installed app too. Out of scope.
- **Job objects.** Kill-on-close binds *children* to us; here we are the child, and the
  launcher (`dotnet`) is not ours to configure.
