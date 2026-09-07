---
title: "Session: 2026-09-06 — Settings flicker: two bugs, one report"
type: session
status: complete
tags: [localization, avalonia, mvvm, communitytoolkit, bugfix, ui]
created: 2026-09-06
summary: "User reported the interface-language picker's rows flickering on selection. First fix (ContentControl tearing down the whole settings page on CultureChanged) was real but did not resolve the report. Root cause, found after web research confirmed CommunityToolkit.Mvvm's AsyncRelayCommand semantics: the picker's four rows share one async command, which CommunityToolkit disables app-wide (per command) while running. Both fixed, both regression-tested, both cross-linked in memory/knowledge."
---

# Session: 2026-09-06 — Settings flicker: two bugs, one report

## Active Focus

`src/Parlotype.Desktop/ViewModels/SettingsWindowViewModel.cs` (`RebuildNavItems`) and
`src/Parlotype.Desktop/ViewModels/Settings/InterfaceLanguageSettingsViewModel.cs`
(`SelectLanguageCommand`). Continuation of the ADR-064 localization work
([[2026-09-05-2140-ui-localization-foundation]]), triggered by the user testing the shipped
feature and reporting a visual defect with a screenshot.

## Decisions Made

- **Bug 1 (real, but not the report): `RebuildNavItems` recreated the content pane.**
  It ran on every `Localizer.CultureChanged` via `NavItems.Clear()` + re-`Add()`. The left
  `ListBox`'s two-way `SelectedItem` binding deselects on an `ItemsSource` reset, so
  `SelectedNavItem` briefly went `null`, `SelectedSection` (bound to
  `ContentControl.Content`) followed, and the `ContentControl` tore down and rebuilt
  whichever settings page was open — a fresh `InterfaceLanguageSettingsView` with fresh
  `Button`s. Fixed by appending the new rows, reselecting a row already in the collection,
  then removing the old ones — never letting the still-wanted row go missing.
- **Bug 2 (the actual report): the picker's four rows share one `async Task` command.**
  `SelectLanguageCommand` is built once in the constructor and handed to every
  `UiLanguageDisplayItem`. CommunityToolkit.Mvvm's generated `AsyncRelayCommand` sets
  `CanExecute = false` for as long as it is running (`IsRunning`) unless
  `AllowConcurrentExecutions` is set. Avalonia's `Button` re-evaluates `CanExecute` on
  `CanExecuteChanged`, so all four rows disabled and re-enabled together for the length of
  the settings write — a uniform flash across the whole list, matching the report exactly.
  Fixed with the pattern already established for `WhisperModelSettingsViewModel.SelectModel`:
  keep the command synchronous, fire-and-forget the async apply.
- Both fixes are regression-tested with a technique that proves the test actually catches
  the bug, not just that it passes: `git stash` the fix, confirm the test fails against the
  reverted code, `git stash pop`, confirm it passes.

## Facts Learned

- A bare-view-model test cannot observe either bug. Bug 1 needs a real `ListBox` bound to
  the collection (nothing deselects `Clear()` without one) — the test instantiates the
  actual `SettingsWindow` and compares the rendered view's identity before/after. Bug 2
  needs the command's runtime *type*, not its behavior over time — a mock settings service
  completes synchronously, so the `IsRunning` window collapses to nothing observable by
  timing; `Assert.IsNotAssignableFrom<IAsyncRelayCommand>(command)` is the only thing that
  actually distinguishes the fixed and broken code.
- This exact command-sharing pattern (`asyncrelaycommand-flicker`) had already bitten the
  project once, in Whisper model selection, and was already documented in
  `memory/knowledge/`. It should have been the first thing checked against the report
  ("list flickers on selection") rather than found by tracing `CultureChanged` subscribers.
- Web search confirmed CommunityToolkit.Mvvm's documented behavior directly (its own GitHub
  docs): `AllowConcurrentExecutions` defaults to `false`, and the toolkit's own guidance for
  "many buttons sharing one async command" is exactly a `CanExecute` predicate that ignores
  `IsRunning`, or — the simpler route already used elsewhere in this codebase — keep the
  command synchronous.

## Open Blockers

None. The user has not yet re-tested the running app after Bug 2's fix (they confirmed Bug
1's fix alone did *not* resolve the report, prompting this session, but has not confirmed
after the actual fix). Verification here is by test (both regression tests fail on the
reverted code and pass on the fix — proven via `git stash` in both directions), not by a
manual run.

## Documentation Status

- ADR: none required — a bug fix within ADR-064's existing architecture, not a new
  decision; no new Core type, DI registration, dependency, or OS-conditional behavior.
- Vault: done — `memory/architecture/subsystems.md`'s Localization section gained a
  paragraph naming both bugs and their fixes, cross-linking both knowledge entries.
- Knowledge: done —
  `memory/knowledge/avalonia-itemssource-clear-deselects-and-recreates-content.md` (new)
  and `memory/knowledge/asyncrelaycommand-flicker.md` (updated with the second occurrence);
  each cross-references the other, since they produced a similar-sounding symptom in the
  same feature and fixing one without testing against the actual report cost a round trip.

## Next Action

This bug-fix increment is done and both fixes are committed
(`cb4f1b2`, `c344cd6`). The broader UI localization plan
([[../../plans/2026-09-05-ui-localization/task]]) is still `in_progress`: phase 6's polish
remains — pseudo-locale (`qps-ploc`), ICU-localized speech-language names (needs the
Russian accusative case fix in `Language_ToggleSwitch_TranslateToFormat` per
`terminology.md`), `docs/localization.md`, and a website note. See
[[2026-09-05-2140-ui-localization-foundation]]'s Next Action for that list — unchanged by
this session.

If the user reports the picker still flickers after this fix, the next things to check are:
`UiLanguageDisplayItem`'s `Border` selection-indicator transition (Fluent theme default
`Transitions` on `Background`/`Opacity`), and whether `RefreshLabels()` (called from
`InterfaceLanguageSettingsViewModel.OnCultureChanged`) causes a measurable reflow of the
"System default" row's `Detail` line that shifts the rows below it.
