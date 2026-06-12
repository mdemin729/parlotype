---
title: Avalonia Popup patterns (headless capture, DataContext swap, light dismiss)
type: knowledge
status: active
tags: [avalonia, popup, headless, screenshots, bindings]
created: 2026-06-11
summary: Headless CaptureRenderedFrame excludes the popup layer; swapping DataContext on an element rebases its other bindings; light-dismiss consumes the anchor click
---

# Avalonia Popup patterns

Learned while building the Language popover pickers and the Transcribe flyout (ADR-036).

- **Headless `Window.CaptureRenderedFrame()` does not include the popup
  layer.** Screenshot tests of popover UI must render the popover *content
  control* directly in its own window instead of opening the `Popup`.
- **Setting `DataContext` on an element rebases every other binding on that
  element.** `<PickerView DataContext="{Binding TargetPicker}"
  IsVisible="{Binding Relationship.IsFullForm}"/>` fails to compile
  (`AVLN2000`) because `IsVisible` now resolves against the picker VM. Wrap in
  a `Panel` that carries the `IsVisible` binding and swap the context on the
  child.
- **Light dismiss consumes the click that lands on the anchor button**
  (`OverlayDismissEventPassThrough` defaults to false), so a "toggle" button
  that opens its own popup closes it on the second click without re-firing the
  command — the natural open/close toggle works without extra state.
- **Two-way `Popup.IsOpen` bindings** let the VM own open state while light
  dismiss / Escape write `false` back; pair with a `bool` `[ObservableProperty]`
  per popover.
- `Popup` chrome conventions used here: shared `Border.popoverChrome` app style
  (background/border/radius/shadow); hosts set width per surface (page 300px,
  widget flyout 268px). Content `UserControl` stays chrome-free so multiple
  hosts can size it.
