---
title: Drain, don't just Dispose, fire-and-forget async work in an [AvaloniaFact] test
type: knowledge
tags: [avalonia, testing, async, dispatcher, threading, flaky]
created: 2026-09-13
last_updated: 2026-09-13
summary: Disposing an object that launched fire-and-forget async work only cancels it — cancellation merely schedules the rest of that work onto the one process-wide headless dispatcher every [AvaloniaFact] shares, so it can still be queued when a later, unrelated test starts pumping that same dispatcher. Await a no-op Dispatcher.UIThread.InvokeAsync after Dispose to drain the queue before the test returns.
---

# Drain, don't just Dispose, fire-and-forget async work in an `[AvaloniaFact]` test

Found while testing [[../decisions/_index|ADR-068]]'s `TranscribeAutoHideController`, and
a second instance of the hazard class in
[[culture-changing-tests-need-avaloniafact|Culture-changing tests need AvaloniaFact]] —
"leftover async work outlives the test that started it and corrupts a later, unrelated one
on the shared headless dispatcher."

## The mechanism

`TranscribeAutoHideController.StartCountdownIfIdle()` launches `RunCountdownAsync`
fire-and-forget (`_ = RunCountdownAsync(cts)`). In `TranscribeWindowAutoHideTests`, a
`ManualCountdown` test double stands in for the real delay so a test can resolve or cancel
the countdown by hand instead of sleeping 1.5 real seconds — its `Delay` method returns a
`TaskCompletionSource` created with `TaskCreationOptions.RunContinuationsAsynchronously`.

When a test *cancels* the countdown (a click, a busy transition, or simply disposing the
controller) rather than letting it elapse, `Dispose()` calls `CancellationTokenSource.Cancel()`
and returns. But `RunContinuationsAsynchronously` means cancelling the `TaskCompletionSource`
only **schedules** the remainder of `RunCountdownAsync` — the `catch (OperationCanceledException)`
block and everything after it — onto the dispatcher; it does not run it inline. And the
dispatcher every `[AvaloniaFact]` test in the assembly runs on is not the disposed
controller's dispatcher, or even that test class's — it is **one process-wide headless
dispatcher thread**, shared by the whole test run.

So `Dispose()` returns before that scheduled continuation actually runs. If the test method
returns right after `Dispose()`, the continuation is still sitting in the dispatcher's queue
— and it fires whenever the *next* test (any test, in any class, that happens to pump the
shared dispatcher next) does so, potentially touching objects that test never created and
has no reason to expect activity from.

## The fix

A small helper that disposes and then drains the queue before the test returns:

```csharp
private static async Task DisposeControllerAsync(TranscribeAutoHideController controller)
{
    controller.Dispose();
    await Dispatcher.UIThread.InvokeAsync(() => { });
}
```

`Dispatcher.UIThread.InvokeAsync(() => { })` queues a no-op *after* whatever `Dispose()`
just scheduled and awaits it — so by the time it completes, everything `Dispose()` queued
has already run. Every test in `TranscribeWindowAutoHideTests` calls this instead of a bare
`controller.Dispose()`.

This is the mechanism that made the full `Parlotype.Desktop.Tests` run hang under real
machine contention despite every individual test disposing its own controller and closing
its own window — the corruption only ever showed up as a failure in some *later*,
unrelated test, exactly like the culture-changing case below.

## The general rule

**Disposing an object that started fire-and-forget async work is not the same as that work
having finished** — `Dispose` (or `Cancel`) only guarantees the work will *stop*, not that
it has *already* stopped, whenever the continuation after the awaited point is scheduled
rather than run inline. Under `Avalonia.Headless`, "scheduled" almost always means "queued
on the one shared dispatcher thread every `[AvaloniaFact]` in the process pumps" — so a
continuation left unawaited by one test is a live hazard for whichever other test pumps
that dispatcher next, not just a benign leak.

This is the same hazard class as
[[culture-changing-tests-need-avaloniafact|Culture-changing tests need AvaloniaFact]]'s
third occurrence (a bare `await Task.Delay` resuming on the thread pool and racing other
parallel `[AvaloniaFact]` tests on the shared dispatcher) — that note is about an `await`
inside the test itself escaping the dispatcher; this one is about work a **disposed
collaborator** left scheduled on it. Both are instances of the same broader rule: treat the
headless dispatcher as shared, mutable, cross-test state, and never let a test return while
something it caused is still only *scheduled* to run there. When in doubt, drain with a
no-op `await Dispatcher.UIThread.InvokeAsync(() => { })` before asserting the test is done.
