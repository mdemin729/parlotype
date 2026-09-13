---
title: "Session: 2026-09-13"
type: session
status: complete
tags: [desktop, transcribe-window, adr-068, documentation, memory-vault]
created: 2026-09-13
summary: "Documentation and closeout pass for ADR-068 (Transcribe widget auto-hide) — code and tests were already done and verified; reconciled ADR-068/vault against the shipped code, added two knowledge entries, closed out the plan"
---

# Session: 2026-09-13

## Active Focus
- [docs/decisions/068-transcribe-window-auto-hide.md](../../docs/decisions/068-transcribe-window-auto-hide.md) — reconciled against shipped code
- [plans/2026-09-13-transcribe-window-auto-hide/task.md](../../plans/2026-09-13-transcribe-window-auto-hide/task.md) — closed out
- [plans/INDEX.md](../../plans/INDEX.md) — moved the plan row to Completed
- [memory/services/desktop.md](../services/desktop.md), [memory/architecture/subsystems.md](../architecture/subsystems.md) — corrected mechanism wording
- [memory/knowledge/avalonia-getobservable-subscribe-trap.md](../knowledge/avalonia-getobservable-subscribe-trap.md),
  [memory/knowledge/avaloniafact-drain-dispatcher-after-dispose.md](../knowledge/avaloniafact-drain-dispatcher-after-dispose.md) — new
- No files under `src/` were touched this session — the feature's code and tests were
  already complete and verified (`dotnet build` clean, `dotnet test` green: Benchmark.Tests
  114/114, Tests 688/688, Desktop.Tests 521/524 + 3 skipped) before this session started.

## Decisions Made
- Verified every claim in ADR-068 and the vault (`memory/services/desktop.md`,
  `memory/architecture/subsystems.md`, `memory/decisions/_index.md`) against the actual
  merged code in `src/Parlotype.Desktop/` — `TranscribeAutoHideController`, `TranscribeWindow`
  (`UserEngaged`/`HideWithFadeAsync`/`AbortFade`/`RestoreChrome`/`FadeDuration`),
  `IWindowManager.ShowTranscribeForDictation()`, `TranscribeViewModel.IsDictationBusy`/
  `IsInErrorState`. The state-machine description, defaults (1.5 s delay / 160 ms fade),
  rejected-alternatives section, and ownership rules all matched the shipped code exactly —
  no factual corrections needed there.
- The one real gap: ADR-068's Consequences section and `memory/services/desktop.md` both
  described the window's hover/visibility watch with the vague phrase "window
  pointer/visibility observables" — technically not false, but it reads as the plan's
  original `GetObservable(...).Subscribe(...)` sketch, which never shipped because it does
  not compile (CS1660 + CS0122). Corrected both to name the actual mechanism: a single
  `AvaloniaObject.PropertyChanged` subscription filtered on `IsPointerOverProperty`/
  `IsVisibleProperty`. Added a dedicated "Implementation note" subsection to ADR-068's
  "Two-tier interaction" section spelling out why the naive form doesn't compile, and a
  matching clarification in `memory/architecture/subsystems.md`'s "Two-tier interaction"
  bullet.
- Recorded, for the record, that the plan's four open questions were settled exactly as
  ADR-068 already states: no setting, fade-out only (never fade-in), transparency rejected,
  two-tier interaction (hover pauses, click/drag/flyout commits). No change was needed to
  the ADR's own wording of these — they were already accurate.
- Chose **not** to rename `memory/knowledge/culture-changing-tests-need-avaloniafact.md`
  even though its lesson generalizes beyond culture-changing tests — six existing files
  (including `docs/decisions/064-ui-localization-foundation.md` and two prior session notes)
  wikilink it by that exact filename. Instead added a "Fourth occurrence, generalized"
  section to that note cross-linking the new drain-dispatcher note, and broadened its
  frontmatter `summary`/`last_updated` to name the shared-dispatcher rule explicitly.
- `plans/INDEX.md`: followed the repo's actual convention — a `## Completed` table at the
  top, newest first — rather than `plans/WORKFLOW.md`'s literal "remove the row" instruction;
  every recent plan closeout in this file already does it this way.
- Left `plans/2026-09-13-transcribe-window-auto-hide/task.md`'s "Manual pass on Windows"
  workplan item unticked and added an "Outstanding" section naming exactly what still needs
  a human at a real desktop, per the task instructions — it genuinely has not been done, and
  the plan's own research.md flags `IsPointerOver` on a frameless transparent window as
  needing empirical verification.

## Facts Learned
- **Avalonia 12 compile trap**: `someAvaloniaObject.GetObservable(prop).Subscribe(lambda)`
  cannot compile outside `Avalonia.Base` — `IObservable<T>` declares its own
  `Subscribe(IObserver<T>)` so dot-style member lookup never reaches the `Action<T>`-taking
  extension (CS1660), and that extension's declaring type `Avalonia.Reactive.Observable` is
  `internal` to `Avalonia.Base` so it cannot be called fully qualified either (CS0122). The
  fix that shipped: one `AvaloniaObject.PropertyChanged` subscription filtered by property.
  Captured in [[../knowledge/avalonia-getobservable-subscribe-trap]].
- **AvaloniaFact test-isolation hazard, generalized**: disposing an object that started
  fire-and-forget async work only cancels it — cancellation *schedules* the remaining
  continuation onto the one process-wide headless dispatcher every `[AvaloniaFact]` in the
  assembly shares, so it can still be queued when a later, unrelated test starts pumping
  that dispatcher. The fix pattern is to drain with a no-op
  `await Dispatcher.UIThread.InvokeAsync(() => { })` after disposing. This is the same
  hazard class as the existing `await Task.Delay`-inside-a-culture-test note, just triggered
  by a disposed collaborator instead of the test body's own `await`. Captured in
  [[../knowledge/avaloniafact-drain-dispatcher-after-dispose]], cross-linked with
  [[../knowledge/culture-changing-tests-need-avaloniafact]].
- `pwsh scripts/check-localization.ps1` passes unchanged, confirming NFR-1 — this feature
  added zero user-facing strings and touched no `.resx` file.

## Open Blockers
- None. The manual Windows pass completed on 2026-09-13 and **all four items pass**:
  hover-pause on the real frameless transparent window, the ~160 ms fade, FR-8's sticky
  card on a cloud misconfiguration (including the self-heal on the next clean dictation),
  and the onboarding tour. Items 1 and 2 also implicitly prove Windows does not activate a
  window shown with `ShowActivated = false` — the risk that would have silently disabled
  the whole feature.

## Documentation Status
- ADR: done — `docs/decisions/068-transcribe-window-auto-hide.md` amended with the
  `GetObservable`/`Subscribe` implementation note and corrected Consequences wording;
  `docs/decisions/040-frameless-compact-transcribe-window.md`'s "Hide, don't close"
  annotation to ADR-068 was already present and correct
- Vault (services/architecture): done — `memory/services/desktop.md` and
  `memory/architecture/subsystems.md` corrected (mechanism wording only; the rest of both
  entries already matched shipped code); `memory/decisions/_index.md`'s ADR-068 row was
  already accurate, no change needed
- Knowledge (non-derivable facts): done — two new entries plus a cross-link generalization
  of an existing one, all indexed in `memory/knowledge/_index.md`

## Next Action
Nothing outstanding on ADR-068 — code, tests, docs and the manual pass are all done, and the
work is committed on a branch with a PR open (see Active Focus). If the PR review raises
anything, the two design points most likely to attract comment are both deliberate and
documented in ADR-068's rejected-alternatives section: `Window.Activated` is not an
engagement trigger, and there is no setting to disable auto-hide (opening the widget from
the tray, or clicking it once, is the escape valve).

The one known follow-up, explicitly out of scope here: FR-8 leaves a sticky card after a
failed session, but ADR-040 made status tooltip-only, so the reason is not visible on the
card itself. Surfacing it would need a new UI affordance plus keys in en/ru/es. Worth its
own plan only if the mute card proves irritating in daily use — it was accepted as-is
during the manual pass.
