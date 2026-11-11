---
title: "Avalonia AXAML inline-text gotchas"
area: "avalonia"
summary: "An attribute value starting with { is parsed as a markup extension (escape it with {}), and sibling Run elements on separate lines get an implicit space."
verified: 2026-11-11
---

# Avalonia AXAML inline-text gotchas

Hit while writing explanatory copy for [PromptSettingsView](../../src/Parlotype.Desktop/Views/Settings/PromptSettingsView.axaml)'s
"How prompts work" panel (literal `{speech_lang}` / `{text_lang}` placeholder names in
`TextBlock`/`Run` text).

## Brace-prefixed attribute values are markup extensions

`Text="{speech_lang}"` is parsed as a markup extension lookup (like `{Binding ...}`),
not the literal string. To emit a literal leading brace, escape with `{}`:

```xml
<Run Text="{}{speech_lang}" FontWeight="SemiBold" />
```

Mid-string braces (`Text="...the {speech_lang} placeholder..."`) need no escaping —
only a value that *starts* with `{` triggers extension parsing.

## Adjacent `<Run>`s on separate source lines get an implicit space

Avalonia's XAML text-content whitespace handling inserts a space between sibling
`<Run>` elements when they're written on separate lines (normal formatting). A `Run`
whose text starts with closing punctuation (`)`, `;`, `,`) therefore renders with a
visible gap before it — the punctuation reads as detached from the previous word.
Word-order the sentence so every `Run` boundary falls on a natural word gap, not
before punctuation.
