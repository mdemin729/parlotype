---
title: Named Sync Primitives for Single-Instance Locks
type: knowledge
status: active
tags: [dotnet, windows, unix, mutex, eventwaithandle, single-instance, testing]
created: 2026-08-02
summary: .NET named mutex/event behaviour that decides how a single-instance guard is written and tested — thread affinity, abandonment, Unix gaps, auto-reset queuing
---

# Named Sync Primitives for Single-Instance Locks

Learned while building `SingleInstanceGuard` ([[055-single-instance-guard|ADR-055]]).
None of this is visible from the code that uses these types.

## Mutex ownership is per-thread, not per-process

A named `Mutex` is held by the **thread** that waited on it. Consequences:

- A second `WaitOne(TimeSpan.Zero)` **on the same thread succeeds** — mutexes are
  recursive. A unit test that acquires twice from the test method therefore proves
  nothing; the second acquisition has to run on its own thread to model a second
  process.
- `ReleaseMutex()` from a thread that does not own it throws `ApplicationException`.
  A `Dispose()` that may run on any thread must catch it.
- Ownership is released when the owning thread exits, even without `ReleaseMutex`.
  Process exit therefore always frees the lock — nothing to time out or clean up.

## AbandonedMutexException means you got the lock

When the previous owner died without releasing (crash, `Stop-Process`), the next
wait throws `AbandonedMutexException` — but **the wait succeeded and the caller now
owns the mutex**. Treating it as a failure would leave the app unstartable until
reboot. Catch it and continue as the owner.

## Named events are Windows-only; named mutexes are not

- `Mutex(bool, string)` works cross-process on Unix (.NET maps it to shared state
  under the runtime's tmp dir).
- Named `EventWaitHandle` / `Semaphore` throw `PlatformNotSupportedException` on
  Unix, and `EventWaitHandle.TryOpenExisting` is annotated
  `[SupportedOSPlatform("windows")]` — with `TreatWarningsAsErrors` an
  `OperatingSystem.IsWindows()` guard is mandatory, not just prudent.
- `Local\` (per logon session) and `Global\` are Windows namespace prefixes. Unix
  has no equivalent, so the name is used bare there.

## Auto-reset events queue exactly one signal

`EventResetMode.AutoReset` stays signalled until *someone* waits, so a `Set()` that
arrives **before** the listener thread starts is delivered when it does. That is
what lets a second launch signal a primary that is still booting without any retry
loop — provided the primary creates the event as early as possible (at lock
acquisition, not when the listener starts).

## Related

- [[sharphook-suppress-event]] — why several live hooks make hotkey delivery
  undefined, the problem the lock exists to prevent.
