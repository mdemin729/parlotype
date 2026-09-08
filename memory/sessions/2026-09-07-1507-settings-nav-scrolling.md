---
title: "Session: 2026-09-07 — Settings nav pane scrolling"
type: session
status: complete
tags: [avalonia, settings-window, scrolling, ui]
created: 2026-09-07
summary: The settings side menu never scrolled (ListBox stacked in a StackPanel) and hid its last rows; gave it a real viewport in the app's own scrollbar, edge fades, and a selection that survives the window being reused (ADR-067).
---

# Session: 2026-09-07

## Active Focus
- `src/Parlotype.Desktop/Views/SettingsWindow.axaml` (+ code-behind) — the nav pane
- `src/Parlotype.Desktop.Tests/SettingsWindowNavScrollTests.cs` — new headless tests (5)
- `docs/decisions/067-settings-nav-pane-scrolling.md` — new ADR

Reported symptom: with Whisper selected the nav list is long enough to run off the
bottom of the pane, and the user has to resize the window to reach Data/Help —
there was no scrollbar and no other hint that rows existed below the fold.

Two review rounds followed the first fix, and both changed it: the user asked why
the scrollbar didn't match the rest of the app (it shouldn't have — see below),
and a code review found the reused-window deep-link gap plus the missing ADR.

## Decisions Made
- **Pane layout is a `DockPanel`, not a `StackPanel`.** The stack measured the
  ListBox with infinite height, so it never scrolled — it just overflowed and got
  clipped. Heading docked top, list fills the rest and scrolls inside a real viewport.
- **Non-virtualizing `ItemsPanel`.** ~20 short rows never need recycling, and an
  exact (non-estimated) extent gives smooth pixel scrolling plus a thumb that
  doesn't resize as it travels.
- **Stock Fluent scrollbar.** First pass pinned it open with
  `AllowAutoHide="False"` and then re-skinned it (slim rounded thumb) because
  pinning snaps Fluent to its wide expanded bar. Both were dropped on review: at
  rest Fluent already draws a visible thin line whenever content overflows —
  pixel-identical to the section pane's — so the pane only ever needed a
  viewport. One scrollbar style across the app beats a nicer one in a single list.
- **Edge fades driven from code-behind.** Avalonia has no "can scroll up/down"
  binding, so `SettingsWindow` listens to the routed `ScrollChanged` and swaps an
  `OpacityMask` (three gradient brushes in `Window.Resources`) onto the ListBox.
  View state, so it stays out of the view model.
- **ADR-067** records the whole thing. Initially judged not to need one (no new
  Core type, DI entry, dependency, P/Invoke or OS-conditional behaviour, and no
  user-facing copy, so no resx work either) — code review disagreed, reading the
  Definition of Done's "touches the settings subsystem" trigger as covering
  behavioural logic in the settings window. Fair: the reopen rule is a real
  behavioural contract, and the two rejected scrollbar approaches are worth
  recording so they are not re-proposed.
- **The reused window is deep-linked while hidden.** `WindowManager.ShowSettings`
  hides rather than closes and calls `NavigateTo` before `Show()`, so
  `AutoScrollToSelectedItem` fires against an unlaid-out list and the pane returns
  at its old offset. `SettingsWindow` re-asserts the selection on
  `IsVisibleProperty`, queued at `DispatcherPriority.Loaded`. Fixing the ordering
  in `WindowManager` instead was rejected — it still races layout, and it spreads
  the invariant across callers.

## Facts Learned
Distilled to [[avalonia-listbox-scroll-affordance]]: the StackPanel/infinite-height
trap (a missing scrollbar means a missing viewport — Fluent's resting bar is
already visible), `AllowAutoHide="False"` meaning "stay expanded" rather than
"stay visible", `ScrollBar` requiring only `PART_Track` if a custom bar is ever
needed, masking the viewport-sized control rather than the scrolled content, and
virtualized extents being estimates that make scroll-to-bottom assertions flaky.

## Open Blockers
- None.

## Documentation Status
- ADR: done — `docs/decisions/067-settings-nav-pane-scrolling.md` (+ index row).
- Vault (services/architecture): none required — no new public symbols.
- Knowledge: done — `memory/knowledge/avalonia-listbox-scroll-affordance.md`.

## Next Action
Nothing outstanding. If a slimmer scrollbar is ever wanted, it belongs in
`App.axaml` (plus `TestApp.axaml`, whose resource scope is separate — see
[[avalonia-resource-scope-in-headless-tests]]) so every list changes together,
never in one view.
