---
title: Configurable transcription prompts for the Gemma 4 engine
status: completed
created: 2026-05-22
started: 2026-05-22
completed: 2026-05-22
---

# Configurable Gemma 4 prompts

## Problem

When the Gemma 4 (llama.cpp) engine is active, every transcription sends a
single hardcoded instruction — the `TranscriptionPrompt` const in
[LlamaCppSpeechRecognizer](../../src/Parlotype.Platform/Speech/LlamaCppSpeechRecognizer.cs).
Users had no way to create, edit, or switch prompts. Whisper has no equivalent
free-text prompt (it uses boolean toggles, ADR-021), so this is Gemma-only.

## Approach

A dedicated registry (`IPromptTemplateRegistry` in Core,
`JsonPromptTemplateRegistry` backing `prompts.json` in Platform) modelled on the
existing `JsonLlamaServerRegistry` — settings stores only the active id
(`ActivePromptId`). A non-deletable built-in default is merged in at read time
so a working prompt always exists. `LlamaCppSpeechRecognizer` resolves the
active prompt per `TranscribeAsync` call (no model reload) and substitutes the
forward-compat `{language}` placeholder with a fixed `"English"` default.

A new `PromptSettingsViewModel` + `PromptSettingsView` (SpeechEngine category,
Gemma4-restricted, ADR-028) provides inline create/edit/duplicate/delete/select.

The shared keyboard-layout source-language setting (common to Whisper + Gemma)
is **out of scope** — only the `{language}` substitution seam is built. See
ADR-030.

## Workplan

- [x] Core: `PromptTemplate` record (`{language}` token + `Render`),
      `IPromptTemplateRegistry`, `SettingsKeys.ActivePromptId`
- [x] Platform: `JsonPromptTemplateRegistry` (built-in seed, quarantine) + DI
- [x] Wire active prompt into `LlamaCppSpeechRecognizer.TranscribeAsync`
- [x] Desktop: `PromptSettingsViewModel`, `PromptDisplayItem`, `PromptSettingsView`
- [x] Register in `App.axaml.cs`, `SettingsWindowViewModel`, `SettingsWindow.axaml`
- [x] Tests: `JsonPromptTemplateRegistryTests`, `PromptSettingsViewModelTests`,
      nav-ordering + recognizer-ctor updates
- [x] ADR-030, memory vault (core/platform/desktop profiles + decisions index)
