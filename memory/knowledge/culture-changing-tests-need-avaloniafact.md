---
title: Tests that change the interface language must be [AvaloniaFact]
type: knowledge
tags: [avalonia, testing, localization, threading, flaky]
created: 2026-09-06
last_updated: 2026-09-06 (third occurrence)
summary: A plain [Fact] calling Localizer.SetCulture runs on an xUnit worker thread; the notification reaches bindings left behind by earlier UI tests and throws a thread-affinity error — but only under load, so the suite passes alone and fails intermittently in a full run. Also hit via a bare `await Task.Delay` inside an [AvaloniaFact] that calls SetCulture: the continuation resumes off the shared headless dispatcher thread and races other parallel tests
---

# Culture-changing tests need `[AvaloniaFact]`

Found while finishing the localization work ([[../decisions/_index|ADR-064]]).

## The symptom

Thirteen `LocalizationTests` failing with:

> System.InvalidOperationException : The calling thread cannot access this object because a
> different thread owns it.

and the maddening property that **the same code passed and failed on alternate runs**:

- `dotnet test src/Parlotype.Desktop.Tests` — passed.
- both classes together under a `--filter` — passed.
- `dotnet test` (whole solution, three test hosts competing for the machine) — failed,
  then passed, then failed.

## The mechanism

`Localizer.SetCulture` notifies every `LocalizedString`, and those notifications reach
**compiled bindings on controls created by earlier `[AvaloniaFact]` tests**. A closed
window's controls stay subscribed until they are collected, so whether the notification
touches a live `AvaloniaObject` depends on GC timing — hence the flakiness.

A plain `[Fact]` runs on an xUnit worker thread, so those binding updates happened off the
UI thread and tripped Avalonia's thread affinity check.

## The fix, in two parts

1. **`Localizer.SetCulture` marshals** — it raises `CultureChanged` and invalidates entries
   through `Dispatcher.UIThread`, because subscribers rebuild `ObservableCollection`s and
   notify bindings. This is the convention CLAUDE.md already states for background threads.
2. **Every culture-changing test is `[AvaloniaFact]`**, which is what actually made the
   suite deterministic. It also matches reality: the app only ever calls `SetCulture` from
   the UI thread.

Both were needed. The marshalling alone left it flaky.

## Third occurrence: `await Task.Delay` inside a culture-changing `[AvaloniaFact]`

Hit again while fixing a code-review finding (ADR-064 amendment): a new test constructed a
`HotkeySettingsViewModel` and, copying the `await Task.Delay(50); Dispatcher.UIThread.RunJobs();`
idiom from `HotkeySettingsViewModelTests.cs` (there, waiting out a fire-and-forget
`InitializeAsync`), then called `Localizer.Instance.SetCulture(Russian)`.

The symptom was the same shape as before — deterministic failure only in a full
`dotnet test src/Parlotype.Desktop.Tests` run, never when filtered to just this test or
just its class — but the **failing tests were different ones**: unrelated
`LocalizationTests` cases (`Lookup_UsesTheSatelliteForTheCurrentCulture`,
`SettingsNavigation_RebuildsInTheNewLanguage`, a `SettingsWindow` recreation test) started
reading the wrong culture mid-assertion. Bisected by toggling `[AvaloniaFact(Skip = "...")]`
across the six new tests one at a time until a single test, run alone with everything else
skipped, reproduced it.

**The mechanism:** `Task.Delay` schedules its continuation on a real thread-pool timer, not
on the Avalonia dispatcher — `[AvaloniaFact]` puts the test *body* on the headless dispatcher
thread, but does nothing to keep an `await`'s continuation there. Test collections outside
`CultureBoundTests` run in parallel per xUnit's collection model, but Avalonia Headless's
dispatcher is one shared, process-wide thread; the moment this test's continuation resumed
off that thread, its `SetCulture(Russian)` call took the `Dispatcher.UIThread.Invoke(Notify)`
branch instead of running inline, competing with whatever other parallel `[AvaloniaFact]`
test happened to be mid-flight on that shared thread at the same moment — reading `Strings.*`
on either side of the collision.

**The fix:** don't `await` anything real inside a test that calls `SetCulture`, if it can be
avoided. Here it could be: `MockSettingsService` completes every `Task` already-finished, so
an `await` chain built entirely on it never actually yields — the state machine's
`awaiter.IsCompleted` check is true at every step and the continuation runs inline. A
fire-and-forget `_ = InitializeAsync()` built only on such mocks has therefore always finished
by the time the constructor returns, and the delay was never load-bearing for this test —
removing it (making the test plain synchronous) fixed it outright. Verified with three
consecutive clean `dotnet test src/Parlotype.Desktop.Tests` runs.

If a genuine await is unavoidable in a culture-changing test, the correct fix is to get back
onto the dispatcher explicitly before touching `Localizer` — `await Dispatcher.UIThread.InvokeAsync(...)`
around the resumed work, not a bare `await Task.Delay`/`Task.Yield`.

## Related traps hit on the way

- **`[CollectionDefinition]` belongs on a marker class**, not on one of the test classes in
  the collection. With it on a test class, adding a *second* class to that collection
  stopped serializing them. Hence `CultureBoundTests`.
- **`SkipUnless` requires `Skip`** in xUnit v3 — the `Skip` string supplies the message.
  Without it every affected case *fails* with "You must set 'Skip' when you set
  'SkipUnless'", rather than skipping.
- A screenshot/review harness that renders many views is best gated behind an environment
  variable (`PARLOTYPE_LAYOUT_REVIEW=1`) rather than left in the default suite: it leaves
  view trees bound to singletons and makes everything downstream timing-sensitive.
