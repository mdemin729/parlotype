---
title: "Session: 2026-09-05 — The XAML previewer was running the app"
type: session
status: complete
tags: [desktop, avalonia, designer, hotkeys, startup, adr-063]
created: 2026-09-05
summary: "ADR-062's symptom returned after it shipped. The real culprit was Rider's Avalonia XAML previewer running OnFrameworkInitializationCompleted — global hook, microphone, 2.6 GB model — in a dotnet.exe owned by the IDE. Guarded (ADR-063)."
---

# Session: 2026-09-05 — The XAML previewer was running the app

## Active Focus

User ran a master build, "closed" it, and the dictation hotkey still worked — no tray icon,
no window, nothing named Parlotype in Task Manager. Same symptom set ADR-062 was meant to
fix, one PR later.

Files:
- `src/Parlotype.Desktop/App.axaml.cs` — runtime guard + method body flattened
- `src/Parlotype.Desktop.Tests/AppRuntimeLifetimeGuardTests.cs` (new)
- `docs/decisions/063-xaml-previewer-must-not-start-the-app.md` (new)

## Decisions Made

- **Guard `OnFrameworkInitializationCompleted` on `ResolveRuntimeLifetime(ApplicationLifetime,
  Design.IsDesignMode)`** — returns the desktop lifetime or null; null returns before the
  container is built. First statement in the method, so future additions are covered with no
  ordering rule to remember.
- **Two orthogonal conditions deliberately.** `Design.IsDesignMode` names the intent but is
  the previewer's contract, not ours; the lifetime check is structural and ours. Either alone
  is a single point of failure.
- **Rejected** keying on "did `Program.Main` run" (a third vote, couples `App` to `Program`);
  hiding `BuildAvaloniaApp()` (kills XAML preview outright); moving bootstrap into `Main`;
  host-process-name denylists.
- Accepted cost: **XAML previews render in the default theme** — `ApplyTheme` needs the
  container, now behind the guard. Right default for a design surface anyway.
- **ADR-062 stands.** Its orphaned-`dotnet run`-child is real and its fix works; it just was
  not the cause of the reported symptom. ADR-063 says so explicitly.

## Facts Learned

Distilled to `memory/knowledge/avalonia-xaml-previewer-runs-your-app.md`:

- The previewer takes the assembly's entry point **only for its `DeclaringType`**, to find
  `public static AppBuilder BuildAvaloniaApp()`. **`Program.Main` is never invoked** — so no
  `SingleInstanceGuard`, no Velopack hooks.
- `SetupWithoutStarting()` **does** call `OnFrameworkInitializationCompleted()` (it is part of
  `AppBuilder.Setup()`), and leaves `ApplicationLifetime` null — so the
  `is IClassicDesktopStyleApplicationLifetime` block holding our `Exit` cleanup is skipped.
  Side effects start; nothing tears them down. Its `MainLoop(CancellationToken.None)` ends
  only when the IDE kills it, and Rider respawns one per preview refresh.
- A **`libuiohook`-class window** is the fast, unambiguous test for "does this process hold
  SharpHook's global hook".
- **Process evidence**: PID 77708, parent `rider64.exe`, `dotnet.exe … Avalonia.Designer.HostApp.dll
  … Parlotype.dll`, owning `libuiohook`. Shared rolling log showed it arming the ADR-062
  watchdog on `rider64`, prewarming Parakeet fp32, opening the microphone and tracking the
  foreground window — twice concurrently.

Process notes worth keeping:
- **My Bash tool's view of files outside the repo is stale** ([[bash-sandbox-stale-file-view]]) —
  it showed a months-old `settings.json` and I nearly reasoned from it. PowerShell is
  authoritative. Bit me again this session; the memory entry earned its keep.
- ADR-062 was diagnosed from a plausible mechanism that reproduced under a *synthetic* test
  (killing a launcher), never from the user's actual process. The lesson: identify the
  offending process before theorising about how it got there. One `Get-CimInstance
  Win32_Process | Select CommandLine, ParentProcessId` would have found this on day one.

## Open Blockers

- None. Verified against the **real** `Avalonia.Designer.HostApp` on both builds via an
  identical harness: unguarded → owns a `libuiohook` window; guarded → does not. Full suite
  green (1219 tests, 0 warnings).

## Documentation Status

- ADR: done — `docs/decisions/063-xaml-previewer-must-not-start-the-app.md`
- Vault: done — `memory/services/desktop.md` (App entry + Launch), `memory/architecture/subsystems.md`
  (new *The XAML previewer is not a Parlotype run*), `memory/decisions/_index.md` row 063
- Knowledge: done — `memory/knowledge/avalonia-xaml-previewer-runs-your-app.md` + index row;
  cross-linked from `dotnet-run-orphans-and-parent-pid.md`, whose framing is now amended

## Next Action

Open the PR from `claude/xaml-previewer-runs-the-app` (branched fresh off master — PR #24 is
merged and closed). Ask the user to kill any surviving `dotnet.exe` previewer once, then
confirm in Rider that editing a `.axaml` no longer produces a process holding the hotkey.

Not user-facing (dev-only), so no CHANGELOG entry.
