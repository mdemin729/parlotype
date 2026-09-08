---
title: Making an Avalonia ListBox scroll — and look like it can
type: knowledge
status: active
tags: [avalonia, scrollviewer, scrollbar, listbox, controltheme, settings-window]
created: 2026-09-07
summary: A ListBox in a StackPanel silently stops scrolling; AllowAutoHide=False pins Fluent's *wide* bar; ScrollBar needs only PART_Track, so a per-list ControlTheme can slim it; and "can scroll up/down" has no binding — drive it from the routed ScrollChanged.
---

# Making an Avalonia ListBox scroll — and look like it can

Learned while fixing the settings nav pane (`SettingsWindow.axaml`), which hid its
last rows with nothing on screen to say so.

## A ListBox inside a StackPanel cannot scroll

`StackPanel` measures children with **infinite** length along its orientation, so
the ListBox's template `ScrollViewer` gets viewport == extent, shows no scrollbar,
and grows past the bottom of its parent — where the parent clips it. The rows are
not merely unreachable, there is no scrollbar, no wheel response, and no keyboard
scroll either: nothing indicates content exists. `DockPanel` (heading docked,
list filling) gives the list a bounded viewport and everything starts working.

The same trap applies to any scrollable in a `StackPanel`, and it is invisible in
a large window — it only bites at the window sizes where content overflows.

## Fluent's resting scrollbar is already visible — do not pin it

The affordance panic is usually misplaced: whenever content overflows, Fluent's
bar sits there as a thin line at rest, without any pointer interaction, and it is
the *same* line the rest of the app shows. If a list looks like it has no
scrollbar, it almost certainly has no viewport either (see above) — fix the
layout, not the bar.

`ScrollViewer.AllowAutoHide` is an **attached** property, so it can be set
straight on a ListBox — but `False` does not mean "keep the thin line", it means
`IsExpanded` stays true, and Fluent's expanded state is the wide track with
stepper arrows. Pinning it therefore makes one list look unlike every other list
in the app, which is worse than the problem it was reached for.

## If a custom bar is ever genuinely needed

`Avalonia.Controls.dll` (12.0.2) contains exactly one `PART_` name used by
`ScrollBar`: **`PART_Track`**. The Fluent theme's `FluentScrollBarLineButton` /
`PageButton` / `Thumb` are theme resources, not contract, so a `ControlTheme` can
be a `Border` around a `Track` with two transparent `RepeatButton`s and a `Thumb`.
Scoping it to one list works — put `<ControlTheme x:Key="{x:Type ScrollBar}">` in
the **ListBox's own `Resources`** and it reaches the bar two templates down
(ListBox → ScrollViewer → ScrollBar), because template children resolve resources
through their templated parent's chain. Parlotype does **not** do this: a
one-off scrollbar skin is a consistency bug, not a feature.

## "Can scroll up / down" has no binding — use ScrollChanged

There is no property or pseudo-class for "rows are hidden above/below", which is
what an edge-fade affordance needs. `ScrollViewer.ScrollChangedEvent` is routed
and bubbles out of the list's template, so `listBox.AddHandler(...)` catches it —
and it fires for **extent and viewport** changes too, so adding rows or resizing
the window re-evaluates without extra plumbing. `ListBox.Scroll` hands back the
template `ScrollViewer` (cast from `IScrollable`) for the offset/extent/viewport
arithmetic.

Apply the resulting `OpacityMask` to the **ListBox**, never to the items
presenter: a mask is in the masked element's own coordinate space, so masking the
scrolled content makes the gradient travel with the rows. The ListBox is
viewport-sized and stays put. The mask also covers the scrollbar (it is inside
the list's template), which reads as intentional at a ~4 % fade.

## A hidden window does not scroll its selection into view

`ListBox.AutoScrollToSelectedItem` only works on a laid-out list. Change the
selection while the window is hidden — a window that is reused by hiding on close
and re-shown later, with the navigation applied *before* `Show()` — and the
selection moves with nothing to scroll: the list comes back at whatever offset it
had when it went away, selected row off-screen. `Show()` does not re-assert it.

Re-assert it on the visibility change (`IsVisibleProperty` turning true), and
**queue** the `ScrollIntoView` at `DispatcherPriority.Loaded`: visibility flips
before the layout pass, and scrolling an unmeasured list is a silent no-op. The
same reasoning applies to anything that measures a viewport on becoming visible.

## Virtualized extents are estimates — a test trap

With the default `VirtualizingStackPanel`, `Extent.Height` is extrapolated from
realized items and **changes as you scroll**, so `Offset = Extent - Viewport`
does not land at the bottom and an "is the top edge faded now?" assertion fails
sporadically. For a short list (a nav menu), an explicit non-virtualizing
`StackPanel` `ItemsPanel` gives exact extents, smooth pixel scrolling, and a
thumb that stops resizing itself mid-travel.

Related: [[avalonia-resource-scope-in-headless-tests]],
[[avalonia-itemssource-clear-deselects-and-recreates-content]]
