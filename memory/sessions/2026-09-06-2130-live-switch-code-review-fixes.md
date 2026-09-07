---
title: "Session: 2026-09-06 — Code review: the live switch that wasn't, on six surfaces"
type: session
status: complete
tags: [localization, avalonia, mvvm, code-review, testing, adr-064]
created: 2026-09-06
summary: "A code review of the ADR-064 localization work found six surfaces where copy recomputes correctly on every read but never re-raises PropertyChanged on a live culture switch, plus one page whose picker copy was never localized at all. Fixed all six with per-surface regression tests, each verified against the un-fixed code via a targeted temporary revert. Along the way, one new test's `await Task.Delay` inside an `[AvaloniaFact]` calling `SetCulture` corrupted unrelated tests in full-project runs — bisected and fixed."
---

# Session: 2026-09-06 — Code review: the live switch that wasn't

## Active Focus

Six findings from a P2 code review of the ADR-064 localization work, all about the "switching
is live" claim being false for copy composed in C#:

1. `LanguageSelectionSettingsViewModel` + `LanguageRowFactory` — picker headers, target
   sub-hint, special rows and hints, "Recent"/"All languages" group headers: never localized
   at all, plain hardcoded English.
2. `LanguageRelationshipViewModel` — tooltip/summary/label properties recompute correctly but
   never re-raise on culture change; shared by two surfaces, not a settings section, so no
   existing hook.
3. `SpeechEngineSettingsViewModel` / `SpeechEngineDisplayItem` — engine cards' name/description
   captured once at construction into immutable properties.
4. `RuntimeSettingsViewModel` / `RuntimeDisplayItem` — same pattern for Auto/Vulkan/CPU cards,
   plus `RestartRequiredNote` and `UnavailableReason` never refreshed.
5. `TranscribeViewModel` — `StatusText`, `CloudProviderLabel`, `SourceShort`/`TargetShort`, and
   the flyout's `TargetPicker` header never refreshed; no settings-section hook either.
6. `HotkeySettingsViewModel` + row/preset view models — recorder prompt, warnings, and
   `HotkeyBindingItemViewModel`/`HotkeyPresetViewModel` rows compute fresh on every read but
   raise nothing; the "Add" menu's flyout reuses its item containers across opens, so a
   `HotkeyPresetViewModel` with no `INotifyPropertyChanged` at all never updates once bound.

## Decisions Made

- **Reused existing resx keys wherever the English text already matched** something on the
  same page or the Transcribe widget, rather than adding near-duplicates —
  `Settings_Language_SourceCaption`, `Transcribe_TargetPicker_Header`/`_Off`,
  `Language_Source_*`. Only 3 new keys: `Language_Picker_RecentGroup`,
  `Language_Picker_AllLanguagesGroup`, `Language_Target_TranslationHint`. One deliberate small
  copy change: the picker's "Layout detection unavailable" special row now reads the fuller
  shared string ("— auto-detecting instead") instead of a shorter duplicate.
- **`LanguagePickerViewModel.Header` became a `Func<string>` callback**, refreshed inside
  `Refresh()` — matching the class's existing "every input is a callback, not a snapshot"
  design, rather than inventing a new refresh mechanism.
- **Two view models with no settings-section `OnCultureChanged` hook** (`LanguageRelationshipViewModel`,
  `TranscribeViewModel`) subscribe to `Localizer.CultureChanged` directly in their constructors,
  same lifetime as `SettingsSectionViewModelBase`'s own permanent subscription (DI singletons,
  never disposed).
- **Display items captured once at construction** (`SpeechEngineDisplayItem`,
  `RuntimeDisplayItem`) converted `DisplayName`/`Description` to `[ObservableProperty]` —
  reused the exact pattern `UiLanguageDisplayItem` already established (primary-constructor
  param feeds an observable field's default).
- **`TranscribeViewModel.StatusText` needed more than a re-raise.** It's a stored string set
  from ~10 different call sites, several of them long-lived errors (cloud key rejected, quota
  exceeded) that a naive "recompute from IsRecording/RecordingState" would have silently
  replaced with "Ready". Added a private `StatusKind` enum + optional `object? param`,
  remembered alongside the text via a new `SetStatus()` helper; `OnCultureChanged` recomputes
  from that kind. Every existing call site now goes through `SetStatus` instead of assigning
  `Strings.X` directly.
- **`HotkeyPresetViewModel` gained `ObservableObject`** even though every property is a plain
  computed getter — needed only so a bound `MenuFlyoutItem` (reusing its container across the
  "Add" menu's repeated opens) has something to listen to at all.
- **Regression tests assert on `PropertyChanged` notifications or the real bound collection**,
  not on reading the property after the fact — a property that recomputes correctly will pass
  a "read it and check the text" test whether or not the fix exists, since the bug is entirely
  about whether anything *tells a bound view* to re-read it.

## Facts Learned

- [[../knowledge/culture-changing-tests-need-avaloniafact]] (third occurrence): a new
  regression test copied the `await Task.Delay(50); Dispatcher.UIThread.RunJobs()` idiom from
  `HotkeySettingsViewModelTests.cs` to wait out a fire-and-forget initializer, then called
  `Localizer.Instance.SetCulture`. `Task.Delay`'s continuation resumes on a thread-pool thread,
  not the Avalonia dispatcher, so the `SetCulture` call took the cross-thread
  `Dispatcher.UIThread.Invoke` branch and raced whatever other `[AvaloniaFact]` test was
  mid-flight on the one shared headless dispatcher — corrupting *unrelated* `LocalizationTests`
  cases in full-project runs, invisible when filtered to one test or one class. Bisected by
  toggling `[AvaloniaFact(Skip = "bisect")]` across the six new tests until isolating the one.
  Fixed by removing the delay: `MockSettingsService` completes every `Task` already-finished,
  so the constructor's fire-and-forget `InitializeAsync` has, in every practical sense, already
  run by the time the constructor returns.
- Confirmed a real (if minor) UX inconsistency existed before this session: the Language
  settings page's picker copy and the Transcribe widget's quick picker used *different* text
  for the same concepts in some places (e.g. the page never localized its "Off — no
  translation" row at all, while the widget's equivalent was already correctly localized) —
  this review is what surfaced it.

## Open Blockers

None. All six fixes are regression-tested, and each test was verified to fail against a
targeted temporary revert of its fix (not git stash — several fixes share files, so a
temporary in-place revert-and-restore was more precise) before being confirmed passing.
Three consecutive clean `dotnet test src/Parlotype.Desktop.Tests` runs after the
`Task.Delay` fix; full solution `dotnet test` also green (`114 + 673 + 477 passed, 3 skipped`).

No manual run in the actual app — verification is entirely by test, consistent with how this
whole localization effort has been verified across sessions.

## Documentation Status

- ADR: done — second amendment to [[../../docs/decisions/064-ui-localization-foundation]],
  covering all six fixes and the `Task.Delay` test hazard.
- Vault: done — `memory/architecture/subsystems.md`'s Localization section gained a paragraph;
  `memory/decisions/_index.md`'s ADR-064 row got a summary of the second amendment.
- Knowledge: done — `culture-changing-tests-need-avaloniafact.md` updated with the third
  occurrence and the general fix (get back onto the dispatcher explicitly before touching
  `Localizer`, rather than a bare `await Task.Delay`/`Task.Yield`).

## Next Action

This code-review pass is closed. The broader UI localization plan
([[../../plans/2026-09-05-ui-localization/task]]) remains `in_progress` on the same optional
phase-6 polish as before this session: pseudo-locale (`qps-ploc`), ICU-localized speech-language
names (needs the Russian accusative fix in `Language_ToggleSwitch_TranslateToFormat`),
`docs/localization.md`, a website note. Nothing user-facing is known to be untranslated or
stuck in the wrong language after a live switch.

If another "stale text after switching language" report surfaces, the pattern to check first
is the one this session fixed six times: a computed property with no `PropertyChanged` behind
it, or a stored string nobody told to recompute.
