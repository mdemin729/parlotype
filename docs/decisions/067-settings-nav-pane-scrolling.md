---
status: accepted
date: 2026-09-07
---

# 067. The settings nav pane scrolls, in the app's own scrollbar

## Context

The settings window's left-hand nav list was a `ListBox` inside a `StackPanel`. A
`StackPanel` measures its children with **infinite** height along its orientation,
so the list never received a viewport: its template `ScrollViewer` saw
`extent == viewport`, drew no scrollbar, ignored the wheel, and the list simply
grew past the bottom of the pane, where the pane clipped it.

The rows lost that way were not merely awkward to reach — nothing on screen said
they existed. With Whisper selected the nav is at its longest (Language, Whisper
model, Whisper runtime and Whisper output join the shared rows), and Data and Help
fell off the bottom at the shipped 900 × 770 window size. The only way to reach
them was to make the window taller, which a user has no reason to try when the
menu looks complete.

Two further facts shape the fix:

- **A section is often chosen from outside the nav.** `SettingsWindowViewModel.NavigateTo`
  deep-links from the Transcribe strip (Language, the active engine's model page,
  Cloud providers) and from the onboarding wizard.
- **The window is reused.** `WindowManager.ShowSettings` cancels the close and
  hides the window, then on the next request calls `NavigateTo` *before* `Show()`.
  The selection therefore moves while nothing is laid out.

## Decision

### The pane is a `DockPanel`, so the list has a viewport

The "Settings" heading is docked to the top and the list fills what remains. This
is the whole fix for the reported symptom; everything below is about the
affordance and the deep-link case.

### The scrollbar is the stock Fluent one

Explicitly rejected: `ScrollViewer.AllowAutoHide="False"`, and a custom slim
`ScrollBar` `ControlTheme` scoped to the nav list. Both were built and then
removed on review.

Fluent's scrollbar is **already visible at rest** — a thin line, no pointer
interaction needed — whenever content overflows, and it is the same line the
section pane on the right shows. Rendering both panes overflowing at once
confirms they are pixel-identical. A missing scrollbar meant a missing viewport,
not a scrollbar that needed pinning open.

`AllowAutoHide="False"` does not mean "keep the thin line"; it means `IsExpanded`
stays true, and Fluent's expanded state is the wide track with stepper arrows.
Reaching for it made one list look unlike every other list in the app, and the
custom theme written to undo *that* was a consistency bug wearing a nicer skin.
If a slimmer scrollbar is ever wanted, it belongs in `App.axaml` — and in
`TestApp.axaml`, whose resource scope is separate — so every list changes
together.

### The list does not virtualize

An explicit `StackPanel` `ItemsPanel`. Two dozen short rows never need recycling,
and a real (rather than extrapolated) extent buys smooth pixel scrolling, a thumb
that does not resize itself mid-travel, and scroll assertions that are not flaky.

### The list fades against any edge that hides rows

An `OpacityMask` — one of three gradient brushes in the window's resources — is
applied to the list: bottom-only at the top of the list, both edges mid-list,
top-only at the end, none when everything fits. This is the cue the nav needs and
the section pane does not: nav rows stop at a hard pane boundary, whereas a
settings page cut mid-sentence is self-evidently continuing.

Avalonia exposes no "can scroll up/down" state to bind to, so this lives in
`SettingsWindow`'s code-behind, driven by the routed `ScrollViewer.ScrollChanged`
(which also fires on extent and viewport changes, so switching engines and
resizing the window re-evaluate for free). It is view state about a viewport; the
shell view model knows nothing about viewports and should not start.

The mask goes on the `ListBox`, which is viewport-sized. Masking the scrolled
content instead would make the gradient travel with the rows, since a mask is
resolved in the masked element's own coordinate space.

### The selected row is re-asserted into view whenever the window becomes visible

`ListBox.AutoScrollToSelectedItem` handles selection changes in a laid-out list,
but does nothing when the change happens while the window is hidden — which is
exactly the reuse path above. The window would return with, say, Help selected
and only the top rows on screen: the same "the menu looks complete" failure the
layout fix set out to remove, one step further along.

`SettingsWindow` therefore watches `IsVisibleProperty` and, when it turns true,
queues `ScrollIntoView` for the selected row at `DispatcherPriority.Loaded`.
Queued, because visibility flips before the layout pass and scrolling an
unmeasured list is a no-op.

Fixing the *ordering* in `WindowManager` (show, then navigate) was considered and
rejected: it would still race the layout pass, and it would leave every future
caller responsible for getting the sequence right. The view keeps its own
invariant instead.

## Consequences

**Easier**

- Every settings section is reachable at every window size, including the
  shipped default.
- Deep links land on their section whether the window is new, already open, or
  returning from the tray.
- One scrollbar style across the app; a future restyle is a single edit in
  `App.axaml` rather than a per-view removal.

**Harder / accepted**

- The nav list is the only list in the app with edge fades. Justified by the hard
  pane boundary above; a second instance of the pattern should be lifted into a
  shared style rather than copied.
- The fades and the reopen behaviour are code-behind, so they are only covered by
  rendered-view tests, not view-model tests. `SettingsWindowNavScrollTests` holds
  five: the list overflows into a scrollable extent instead of being clipped, each
  fade state matches the scroll position, no mask when everything fits, a
  deep-linked row is brought into view while visible, and the same while hidden
  and reshown.
- Dropping virtualization is safe only because the nav is short and fixed by
  construction. A nav that grew into the hundreds would need it back — and with
  it, the estimated extents that make the fade arithmetic approximate.
