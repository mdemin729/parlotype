---
title: Avalonia Fluent composite-control patterns (SplitButton anatomy)
type: knowledge
tags: [avalonia, fluent-theme, custom-controls, ui]
created: 2026-07-10
summary: How Avalonia's Fluent SplitButton and TextBox themes build multi-part controls that read as one visual frame — reused for Parlotype's ApiKeyBox.
---

Learned reading the Avalonia source (`Avalonia.Themes.Fluent/Controls/SplitButton.xaml`,
`TextBox.xaml`) while building `ApiKeyBox` (`src/Parlotype.Desktop/Views/ApiKeyBox.axaml`).

## SplitButton's "one frame, many parts" trick

`SplitButton`'s template is a 3-column `Grid` (primary `Button` | 1px separator `Border` |
secondary `Button`), but it reads as a single seamless control because:
- Only the **outer edges** get corner rounding — via `LeftCornerRadiusFilterConverter` /
  `RightCornerRadiusFilterConverter` applied to the shared `CornerRadius` template binding
  (left button keeps top-left/bottom-left, right button keeps top-right/bottom-right, the
  middle is square).
- Only the **outer edges** get a border — via a `MarginMultiplierConverter`-based converter
  (`PrimaryButtonBorderMultiplier`/`SecondaryButtonBorderMultiplier`) that zeroes the shared
  side of `BorderThickness` per button, and the separator itself carries a top/bottom-only
  border of the same brush.
- All sub-parts bind `Background`/`BorderBrush` from the *same* template properties, so a
  single set of setters (base/pointerover/pressed/checked/disabled) recolors the whole thing.

For a simpler two-part control (like an entry field + one button, not two buttons), the same
effect is achievable more cheaply: put everything inside **one outer `Border`** (single
background/border/corner-radius) and make every inner part draw *no* chrome of its own in any
visual state — see below.

## Making an inner `TextBox` chrome-less inside a custom frame

`TextBox`'s control template names its background/border element `PART_BorderElement`. To let
an outer `Border` be the only visible frame, override it directly:
```xml
<Style Selector="TextBox#Entry /template/ Border#PART_BorderElement">
    <Setter Property="Background" Value="Transparent" />
    <Setter Property="BorderBrush" Value="Transparent" />
    <Setter Property="BorderThickness" Value="0" />
</Style>
```
Repeat for `:pointerover` and `:focus` — the base Fluent `TextBox` theme sets background/
border/thickness on all three states, so all three need the override or the box "flashes" its
own chrome on hover/focus. Then highlight the *outer* frame instead, e.g.
`Border#Chrome:focus-within` (fires while the inner TextBox has focus, no extra code).

## Built-in reveal-password glyphs are already theme resources

`TextBox.xaml` defines `PasswordBoxRevealButtonData`/`PasswordBoxHideButtonData` as
`StreamGeometry` resources and ships a ready-made `Classes="revealPasswordButton"` on
`TextBox` that adds an in-box eye toggle automatically (bound to `RevealPassword` via
`$parent[TextBox]`). Reuse the same StreamGeometry data for a custom reveal button instead
of sourcing/drawing new icons — swap between the two via a `:checked` selector on visibility,
no color/fill change needed since the glyph itself communicates state.

## `TextControlButton*` brushes for flat auxiliary buttons

`FluentTextBoxButton`/`FluentTextBoxToggleButton` (the clear-button/reveal-button theme) use
`TextControlButtonBackground(PointerOver/Pressed)` and `TextControlButtonForeground*` — reuse
these resource keys for any button that lives *inside* a text-input-like frame, so it matches
the platform's built-in clear/reveal buttons without hardcoding colors.
