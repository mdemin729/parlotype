---
title: Tests that change the interface language must be [AvaloniaFact]
type: knowledge
tags: [avalonia, testing, localization, threading, flaky]
created: 2026-09-06
last_updated: 2026-09-06
summary: A plain [Fact] calling Localizer.SetCulture runs on an xUnit worker thread; the notification reaches bindings left behind by earlier UI tests and throws a thread-affinity error — but only under load, so the suite passes alone and fails intermittently in a full run
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
