---
title: Avalonia resource scope under the headless test host
type: knowledge
tags: [avalonia, testing, resources, styles, screenshots]
created: 2026-09-05
last_updated: 2026-09-05
summary: Three Avalonia scope surprises that render as invisible pixels with no error — App.axaml resources absent under the test TestApp, DynamicResource dead inside Styles setters, and descendant selectors leaking into tooltip content
---

# Avalonia resource and selector scope surprises

Three scope failures that produce **no error, no warning and no failing test** — only
invisible pixels. All three were hit while implementing [[../decisions/_index|ADR-061]];
the third was reported by the user against a shipped build, not caught here.

## 1. `Application.Resources` is empty in headless tests

`Parlotype.Desktop.Tests` registers its own `TestApp : Application`
(`TestAppBuilder` + `[assembly: AvaloniaTestApplication]`), **not** `Parlotype.Desktop.App`.
So anything declared in `App.axaml`'s `Application.Resources` simply does not exist under
`AvaloniaFact`/`AvaloniaTheory` — `{DynamicResource Foo}` resolves to null, the property is
left unset, and the control renders transparent.

Consequence: hoisting a brush from a view's own `Resources` into `App.axaml` is *not* a
free tidy-up. It keeps working in the real app, keeps compiling, keeps every assertion
green, and quietly blanks the colour in every screenshot scenario that was supposed to
verify it. Keep palettes that screenshot tests depend on **in the view that uses them**,
duplicated across views if necessary, and pin them with a `TryFindResource(key, variant, …)`
test per surface (`WarnPaletteResourceTests`).

`Application.Styles` is not affected — `TestApp.axaml` includes `FluentTheme`, so theme
resources (`SystemControlBackgroundBaseLowBrush`, …) resolve normally. Only *app-declared*
resources vanish.

## 2. `DynamicResource` inside a `Styles` setter doesn't see the view's `Resources`

```xml
<UserControl.Resources>… <SolidColorBrush x:Key="WarnForegroundBrush" …/> …</UserControl.Resources>
<UserControl.Styles>
  <Style Selector="Button.paused">
    <Setter Property="Background" Value="{DynamicResource WarnBackgroundBrush}" />  <!-- null -->
  </Style>
</UserControl.Styles>
```

The setter resolves against the style's own scope, not the styled element's, so a
view-local dictionary is not consulted; the setter applies null and the button draws
fully transparent — including its border and its content's foreground, which is *less*
visible than having written no style at all.

Direct property bindings on elements in the same view (`<TextBlock Foreground="{DynamicResource …}">`)
resolve fine. So: `{DynamicResource}` for element properties, **literal colours** in class
styles — which is what the pre-existing `Button.connector.on` (`Value="#378ADD"`) had been
doing all along.

## 3. A descendant selector reaches into the control's tooltip

```xml
<Style Selector="Button.connector.on TextBlock">
  <Setter Property="Foreground" Value="White" />   <!-- also paints the tooltip -->
</Style>
```

A tooltip's content is a logical child of the control it hangs off, so this matched
the `TextBlock` **inside the tooltip popup** as well as the button's own glyph — white
tooltip text on the tooltip's light background, i.e. an unreadable tooltip, in light and
dark alike. Give the styled child an explicit class (`TextBlock.connectorGlyph`) rather
than relying on a bare descendant selector.

Setting `Foreground` on the *button itself* does **not** leak — the tooltip lives in its
own popup root and does not inherit it (verified: the record button's white-on-accent
recording style leaves its hotkey tooltip readable). Only selector matching crosses over.

Related: an Avalonia `ToolTip` left open by a test keeps its show/close timer alive past
the end of that test, and it surfaces as
`InvalidOperationException: Cannot get KeyValueStorage on the idle test context` blamed on
whichever test ran next — so it looks like flakiness in an unrelated test. Close tooltips
in a `finally`.

## How this was caught

Not by a failing test — all 414 passed. The screenshot HTML reports under `reports/` were
extracted and the connector's pixel region sampled: 4200 px of pure `(0,0,0)` where the
working `on` state showed `(55,138,221)`. **When verifying a visual change, read the
pixels, not the test result.**
