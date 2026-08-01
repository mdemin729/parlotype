---
title: Avalonia AXAML inline-text gotchas
type: knowledge
tags: [avalonia, axaml, textblock]
created: 2026-08-01
summary: Attribute values starting with `{` need markup-extension escaping; adjacent <Run> elements on separate lines get an implicit space, so punctuation-leading Runs detach.
---

# Avalonia AXAML inline-text gotchas

Hit while writing explanatory copy for [[../services/desktop|PromptSettingsView]]'s
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
