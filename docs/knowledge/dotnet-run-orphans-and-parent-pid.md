---
title: "dotnet run Orphans WinExe Children; Reading a Parent PID"
area: "windows"
summary: "An abnormally stopped dotnet run can leave the WinExe child running with its global hook live; reading a parent PID needs NtQueryInformationProcess."
verified: 2026-11-11
---

# dotnet run Orphans WinExe Children; Reading a Parent PID

Learned building `ParentProcessExitWatcher` ([ADR-062](../decisions/062-dev-parent-process-watchdog.md)).

## A stopped `dotnet run` can orphan the real app

`dotnet run` executes the app as a **child** process. When the `dotnet` CLI host is
stopped abnormally — closing/killing an integrated terminal, an IDE "stop", a
`dotnet watch` restart race — the child is frequently **not** killed and, being a
`WinExe`, owns no console window and gets no `CTRL_CLOSE_EVENT` either. It keeps
running with a full dispatcher, no window, and a tray icon in the notification-area
overflow. In Task Manager it is **`dotnet.exe`**, not the assembly name, so it looks
like nothing is there.

This is not observable from the code and is intermittent — it depends entirely on
*how* the run was stopped. A clean Ctrl+C in the terminal usually does propagate.

## A background-thread global hook outlives a botched shutdown

SharpHook's `SimpleGlobalHook` with `runAsyncOnBackgroundThread: true` runs on a
`Thread { IsBackground = true }` (confirmed by decompiling `BasicGlobalHookBase.RunAsync`).
That means:

- it never keeps the process alive on its own, and
- it never blocks a real shutdown, **but**
- it also does not stop until the process ends or `hook.Dispose()` is called.

So an orphaned dev process still delivers global hotkeys and injects text. And in an
`async void` `Application.Exit` handler, `hook.Dispose()` must run **before** the
first `await` — a continuation stranded on a stopped dispatcher would otherwise skip
it (harmless for process exit since the thread is background, but the hook stays
registered with the OS for the life of the process).

## Reading a parent PID in .NET

There is **no managed API**. The only reliable way is P/Invoke:

```
ntdll!NtQueryInformationProcess(handle, ProcessBasicInformation /*0*/, ref pbi, size, out _)
```

then `pbi.InheritedFromUniqueProcessId`. Notes:

- `PROCESS_BASIC_INFORMATION` — declare every field `nint`; the two 32-bit fields
  (`ExitStatus`, `BasePriority`) are followed by exactly their alignment padding on
  x64, so pointer-sized fields keep the following members at the right offsets.
- The field records the **creator at spawn time** and is never updated. A resolved
  id can point at an exited process or (after a long time) a reused id — always
  open the handle immediately and verify `!HasExited`.
- Undocumented but stable for decades. `DllImport` is fine here (matches
  `Win32KeyboardLayoutService`); `SYSLIB1054` is not an error in this repo.

## Find the process before theorising

Identify the offending process before theorising about how it got there. One
`Get-CimInstance Win32_Process | Select CommandLine, ParentProcessId` would have found the
Rider XAML previewer on day one; ADR-062 was built on a synthetic launcher-kill repro
instead of the user's real process.

## Ruled out: Avalonia tray icon after an Explorer restart

Avalonia 12.0.2's `Win32.TrayIconImpl` **does** handle the `WM_TASKBARCREATED`
broadcast (with `ChangeWindowMessageFilterEx` on the message window), re-adding every
icon. An Explorer crash/restart is not a cause of a vanished Parlotype tray icon on
this version.

## Related

- [avalonia-xaml-previewer-runs-your-app](avalonia-xaml-previewer-runs-your-app.md) — **the other, more common cause of the same
  symptom**, and the one behind the original report: the XAML previewer runs
  `OnFrameworkInitializationCompleted` in an IDE-owned `dotnet.exe`. Everything in this note is
  still a real failure mode; it just was not that one.
- [named-sync-primitives](named-sync-primitives.md) — the single-instance guard that makes an orphan worse
  (every later `dotnet run` defers to it).
- [sharphook-suppress-event](sharphook-suppress-event.md), [sharphook-modifier-sides](sharphook-modifier-sides.md) — SharpHook behaviour.
