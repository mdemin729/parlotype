---
status: accepted
date: 2026-08-02
---

# 055. One Instance Per Session, With Activation

## Context

Nothing stopped Parlotype from running several times at once. Every extra process
starts a full copy of the app: `SharpHookHotkeyService` installs its own global
keyboard hook, `TranscribeViewModel` opens the same microphone, `JsonSettingsService`
writes the same `settings.json`, and a second tray icon appears.

The hotkey is the part that actually breaks. Each process gets its own
`TaskPoolGlobalHook`, so a single press is delivered to all of them — every instance
starts recording, each with its own model loaded, and whichever finishes first
injects text into the target window. `SuppressEvent` makes it worse rather than
better: whichever hook suppresses the key first decides what the others see. Which
instance answers a keypress is undefined, and so is what ends up typed.

Parlotype makes this easy to hit because it is tray-first with no window on
startup (ADR-040): a launch that "does nothing visible" is indistinguishable from a
launch that failed, so the natural response is to launch it again. The installer's
"run now", a Start-menu tile and a pinned shortcut all lead to the same place.

Two mechanisms were considered:

- **Scan the process list** for another `Parlotype.exe` — what the request suggested.
  It is racy (two launches at login can both scan before either appears), it needs a
  tie-break rule to decide which of two live processes should die, and it matches on
  a name any other program can have. It also cannot tell a stale entry from a live
  one without extra work.
- **A named mutex.** The kernel decides ownership atomically, so exactly one launch
  can win; ownership disappears with the process, so a crash or a kill leaves nothing
  to clean up or time out.

## Decision

`SingleInstanceGuard` (`Parlotype.Desktop/Services/`) takes a named mutex in
`Program.Main`; a launch that does not get it signals the running instance and exits 0.

1. **Placement in `Main`: after `VelopackApp.Run()`, before Avalonia.** Velopack
   re-invokes the same executable for its install, update and uninstall hooks
   (ADR-053) — those must never be turned away as a second instance, and `Run()`
   handles and exits them before the guard is reached. Avalonia starts after, so a
   process that is about to exit never puts anything on screen.
2. **Not a DI service.** The check has to happen before `BuildServiceProvider`
   exists, so the guard is constructed directly and parked on
   `Program.SingleInstance` — the same pattern `Program.TextInjectionMode` already
   uses. Nothing else resolves it, so it gets no `Parlotype.Core` interface.
3. **Session-scoped names** (`Local\Parlotype.SingleInstance`). The conflict being
   prevented is per-session: hotkeys, audio devices and the tray belong to a logon
   session, so a second signed-in user gets their own instance rather than being
   locked out by the first. `Global\` would have been wrong.
4. **The second launch activates the first**, rather than exiting silently. The
   primary owns a named auto-reset event and watches it on a background thread; the
   secondary opens it, sets it, and exits. The primary shows the Transcribe window —
   the same thing a tray-icon click does. Without this, re-launching a tray-only app
   looks broken, which is what produces the second instance in the first place.
5. **The event is created when the mutex is won, not when the listener starts.** It
   therefore exists from the first moment of startup, seconds before the window
   manager does. An auto-reset event stays signalled until someone waits on it, so a
   signal that arrives during startup is delivered when the listener thread starts
   instead of being dropped — no retry loop in the second process.
6. **`AbandonedMutexException` counts as acquiring the lock.** A killed or crashed
   instance leaves the mutex abandoned; the wait still succeeds, and treating that as
   failure would make the app unstartable until reboot.
7. **Fail open.** Any failure to create the mutex — policy, a name collision with
   another kernel object — logs a warning and reports primary. Running twice is a
   nuisance; not starting is a broken app.
8. **Windows only for activation.** Named `EventWaitHandle`s are a Windows primitive
   and .NET throws on Unix, so `CreateActivationEvent` returns null off Windows and
   `SignalPrimary` returns false. macOS and Linux still get the lock — the second
   launch just exits without bringing the first one forward. Named mutexes work on
   both, and no `Local\` prefix is used there.

Logging goes to `VelopackFileLogger` (`velopack.log`), the only logger that exists
this early — ZLogger is configured inside `BuildServiceProvider`.

## Consequences

- **Easier:** Exactly one hotkey listener, one microphone client and one
  `settings.json` writer per session. The undefined-behaviour class described above
  is gone rather than made less likely.
- **Easier:** Re-launching Parlotype now does something visible — it opens the
  Transcribe window — so the launch-again loop that created the problem no longer
  starts.
- **Harder:** Running two builds side by side (a dev build while the installed app
  runs) no longer works; the second exits and activates the first. Deliberate — that
  pair conflicts over the same hotkey — but it changes the debugging workflow: quit
  the installed instance first. `SingleInstanceGuard.Acquire(name)` takes an override,
  used by the tests so a running Parlotype cannot fail a test run.
- **Note:** The guard is per logon session, not per machine. Two signed-in users each
  get an instance, which is correct: their hotkeys and audio devices are separate.
- **Note:** Velopack's update-and-restart path is unaffected. Update.exe waits for
  this process to exit, and mutex ownership is released by process exit, so the
  relaunched app takes the lock cleanly.
- **Verified:** Launched the built `Parlotype.exe`, confirmed a tray-only process with
  no window (`MainWindowHandle == 0`), then launched it again. The second process
  exited with code 0, `velopack.log` recorded *"Parlotype is already running — asked
  it to show itself and exiting"*, one process remained, and the first instance's
  `MainWindowHandle` became non-zero — the window came up. Seven unit tests in
  `SingleInstanceGuardTests` cover the lock, release, abandonment and both activation
  orderings.
