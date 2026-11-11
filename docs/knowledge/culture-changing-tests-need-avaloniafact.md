---
title: "Tests that change the interface language must be [AvaloniaFact]"
area: "avalonia"
summary: "Headless tests share one dispatcher thread: change culture only in [AvaloniaFact], never await in such a test, and drain disposed fire-and-forget work."
verified: 2026-11-11
---

# Culture-changing tests need `[AvaloniaFact]`

Found during the localization work ([ADR-064](../decisions/064-ui-localization-foundation.md))
and the Transcribe auto-hide work ([ADR-068](../decisions/068-transcribe-window-auto-hide.md)).

## The rule

Avalonia Headless runs every `[AvaloniaFact]` in the process on **one shared dispatcher
thread**. Anything a test leaves merely *scheduled* on it runs whenever the next test (any
class) pumps that dispatcher. Every trap below is this rule in a different shape: the
symptom is an unrelated test failing, only in a full run, never when filtered.

## Trap 1: a plain `[Fact]` that calls `Localizer.SetCulture`

`SetCulture` notifies every `LocalizedString`, and those notifications reach compiled
bindings on controls created by earlier `[AvaloniaFact]` tests (a closed window's controls
stay subscribed until collected, so it depends on GC timing). From a plain `[Fact]` this
runs on an xUnit worker thread and throws *"The calling thread cannot access this object
because a different thread owns it."*

The fix took both parts: `Localizer.SetCulture` marshals through `Dispatcher.UIThread`,
and every culture-changing test is `[AvaloniaFact]`. The marshalling alone left it flaky.

## Trap 2: any `await` inside a culture-changing `[AvaloniaFact]`

`[AvaloniaFact]` puts the test *body* on the dispatcher, but does nothing to keep an
`await`'s continuation there. `await Task.Delay(...)` and `await Task.Yield()` both resumed
on a pool thread, so the following `SetCulture` raced other parallel tests on the shared
dispatcher, and unrelated `LocalizationTests` read the wrong culture mid-assertion.

- Usually the await is not needed: `MockSettingsService` and similar mocks return
  already-completed tasks, so a fire-and-forget `InitializeAsync` built on them has finished
  by the time the constructor returns. Make the test synchronous.
- If a result must be awaited, use `.GetAwaiter().GetResult()` (blocks on the dispatcher
  thread) or get back onto it with `await Dispatcher.UIThread.InvokeAsync(...)`.

## Trap 3: disposed collaborators: drain, don't just Dispose

`TranscribeAutoHideController` launches `RunCountdownAsync` fire-and-forget. Its test double
completes a `TaskCompletionSource` created with `RunContinuationsAsynchronously`, so
cancelling it in `Dispose()` only **schedules** the rest of `RunCountdownAsync` (the
`catch (OperationCanceledException)` and everything after) onto the shared dispatcher.
`Dispose()` returns first; if the test returns too, that continuation fires inside some
later test. This is what hung the full `Parlotype.Desktop.Tests` run under machine load.
Dispose, then drain the queue before returning:

```csharp
private static async Task DisposeControllerAsync(TranscribeAutoHideController controller)
{
    controller.Dispose();
    await Dispatcher.UIThread.InvokeAsync(() => { });
}
```

The no-op is queued after whatever `Dispose()` scheduled, so awaiting it means all of that
has run. `Dispose`/`Cancel` guarantees the work will stop, not that it has stopped.

## Related xUnit traps

- **`[CollectionDefinition]` belongs on a marker class**, not on one of the test classes in
  the collection; on a test class, adding a second class stopped serializing them. Hence
  `CultureBoundTests`.
- **`SkipUnless` requires `Skip`** in xUnit v3. Without it every case *fails* with "You must
  set 'Skip' when you set 'SkipUnless'".
- **`Assert.ThrowsAsync<T>` demands the exact exception type.** Switching a throw site to a
  derived type breaks existing `ThrowsAsync<BaseType>` asserts.
- **Search for an existing harness before writing one.** `LocalizedLayoutReviewTests` is
  gated behind `PARLOTYPE_LAYOUT_REVIEW=1` because rendering many views leaves trees bound
  to singletons and breaks the culture-bound tests. An ungated copy written later broke
  them again.
