---
title: "Session: 2026-08-02 — Single instance per session"
type: session
status: active
tags: [desktop, startup, hotkeys, mutex, velopack, adr-055]
created: 2026-08-02
summary: "Nothing stopped several Parlotypes running at once, so one hotkey press reached every instance. Added SingleInstanceGuard (named mutex in Program.Main) plus activation of the running instance (ADR-055)."
---

# Session: 2026-08-02 — Single instance per session

## Active Focus

User report: the app can be launched several times, and it is unclear which copy
answers the hotkey — they may compete for it. Confirmed: each process installs its
own `TaskPoolGlobalHook`, opens the same microphone and writes the same
`settings.json`.

Files: `src/Parlotype.Desktop/Services/SingleInstanceGuard.cs` (new),
`src/Parlotype.Desktop/Program.cs`, `src/Parlotype.Desktop/App.axaml.cs`,
`src/Parlotype.Desktop.Tests/SingleInstanceGuardTests.cs` (new),
`docs/decisions/055-single-instance-guard.md` (new).

## Decisions Made

- **Named mutex, not a process-list scan** (the shape the request suggested).
  Kernel-atomic, so two launches at login cannot both win; ownership dies with the
  process, so a crash leaves nothing stale to detect or time out.
- **Placed after `VelopackApp.Run()` and before Avalonia.** Velopack re-invokes the
  same exe for install/update/uninstall hooks (ADR-053) — those must not be turned
  away as second instances — and a process about to exit must not paint anything.
- **The second launch activates the first**, rather than exiting silently. Parlotype
  is tray-first with no startup window (ADR-040), so a silent second launch looks
  broken — which is exactly what makes users launch it again.
- **The activation event is created when the mutex is won**, not when the listener
  starts, so a signal during the multi-second startup is queued instead of dropped
  and the secondary needs no retry loop.
- **Session-scoped (`Local\`)**, so a second signed-in user gets their own instance.
- **Fail open** everywhere: abandonment counts as acquiring, any other failure logs
  and reports primary. Not starting is worse than starting twice.
- **No Core interface, no DI registration** — the check predates
  `BuildServiceProvider`, so the guard sits on `Program.SingleInstance` the way
  `Program.TextInjectionMode` already does.
- `Acquire(name)` takes a name override, used only by tests, so a Parlotype running
  on the developer's machine cannot fail a test run.

## Facts Learned

Distilled to `memory/knowledge/named-sync-primitives.md`:

- **Mutex ownership is per-thread, not per-process.** A same-thread re-acquire
  succeeds recursively, so a test must model the second process on its own thread;
  `ReleaseMutex` from a non-owning thread throws `ApplicationException`, which
  `Dispose` has to swallow.
- **`AbandonedMutexException` means the wait succeeded** and you own the mutex —
  the killed-instance path, not an error.
- **Named `EventWaitHandle`s throw on Unix; named mutexes do not.** So the lock is
  portable and activation is Windows-only. `TryOpenExisting` is
  `[SupportedOSPlatform("windows")]`, which under `TreatWarningsAsErrors` makes the
  `OperatingSystem.IsWindows()` guard mandatory.
- **Auto-reset events stay signalled until waited on**, which is what makes the
  signal-before-listener ordering safe.

## Open Blockers

None. One thing not exercised here: the installed (Velopack) build — the guard was
verified against `bin/Debug` binaries. The hook path is unaffected by construction
(`VelopackApp.Run()` handles and exits before the guard is reached), but the first
release after this lands is the end-to-end proof.

## Documentation Status

- ADR: done — `docs/decisions/055-single-instance-guard.md`
- Vault (services/architecture): done — `memory/services/desktop.md` (Key Paths +
  Launch), `memory/architecture/subsystems.md` (new *Single Instance & Activation*),
  `memory/decisions/_index.md` row 055
- Knowledge (non-derivable facts): done — `memory/knowledge/named-sync-primitives.md`
- `CHANGELOG.md` deliberately not touched: per ADR-054 the `## [Unreleased]` section
  is drafted at release time by `/release-notes` from git log + ADRs.

## Next Action

Commit on `claude/single-app-instance-fed0e6` and open the PR. When the next release
is drafted, this is user-facing: "launching Parlotype again brings the running one
forward instead of starting a second copy."

Still outstanding from the previous session
([[2026-08-02-1100-release-notes-pipeline]]): the user has not yet been asked about
backfilling `gh release edit` for `v0.4.0`.
