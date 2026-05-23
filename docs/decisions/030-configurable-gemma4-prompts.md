---
status: accepted
date: 2026-05-22
---

# 030. Configurable transcription prompts for the Gemma 4 engine

## Context

When the Gemma 4 (llama.cpp) engine is active, every transcription sends a
single hardcoded instruction to the sidecar — the `TranscriptionPrompt` const
in `LlamaCppSpeechRecognizer`, injected as the `text` content block of the
`/v1/chat/completions` request. Users had no way to change it. We want them to
create, edit, duplicate, delete, and select multiple named prompts via a
Settings page.

This applies **only to Gemma 4**. Whisper drives behaviour through boolean
toggles (`TranslateToEnglish`, punctuation — see ADR-021), not a free-text
prompt, so a free-text prompt editor has no Whisper consumer.

A future, separate feature will derive a **source language** from the active
keyboard layout as a setting shared by both engines. To avoid reworking saved
prompts when that lands, prompts may contain a `{language}` placeholder now.

## Decision

- **Contract (Core).** New `PromptTemplate` record (`Id`, `Name`, `Text`,
  `IsBuiltIn`) with a `LanguageToken = "{language}"` constant, a
  `DefaultLanguage = "English"` constant, and `Render(string?)` that substitutes
  the token. New `IPromptTemplateRegistry` interface
  (`ListAsync`/`GetAsync`/`AddOrUpdateAsync`/`RemoveAsync`/`GetActiveAsync`/
  `SetActiveAsync`). New settings key `ActivePromptId`.
- **Persistence — registry, not settings.** `JsonPromptTemplateRegistry`
  (Platform) backs user prompts with `prompts.json` under
  `%LOCALAPPDATA%/parlotype`, modelled on `JsonLlamaServerRegistry`
  (`SemaphoreSlim`, camelCase JSON, corrupt-file quarantine). The active
  selection lives in `ISettingsService` under `ActivePromptId`. `ISettingsService`
  stores scalars, so a list of complex objects belongs in a dedicated registry —
  the same split already used for managed llama-server installs (ADR-026).
- **Built-in default.** The original Google-prescribed prompt ships as a
  non-deletable, non-editable built-in entry (`builtin-default`). It is merged
  in at read time and never written to disk, so a working prompt always exists
  even if `prompts.json` is hand-edited or deleted. `AddOrUpdateAsync` /
  `RemoveAsync` throw for built-ins; `GetActiveAsync` falls back to it.
- **`{language}` substitution.** `LlamaCppSpeechRecognizer.TranscribeAsync`
  resolves the active prompt per call and renders it with the default language
  before sending. Reading per-call (rather than caching) means a prompt change
  takes effect on the next utterance with **no model reload** — unlike a model
  switch (ADR-017), the sidecar process is untouched.
- **UI.** New `PromptSettingsViewModel` + `PromptSettingsView` (Category
  `SpeechEngine`, `RestrictToEngine = Gemma4`, per ADR-028), with inline
  create/edit (an `IsEditing`-toggled form), duplicate, delete, and select.
  Built-in items disable Edit/Delete.

### Explicitly out of scope

- The shared **source-language-from-keyboard-layout** setting. Only the
  `{language}` substitution seam (single `DefaultLanguage` const) is built now;
  the detector and the shared setting key come later.
- Per-engine or per-model prompt scoping — one active prompt applies to all
  Gemma 4 transcriptions.
- Whisper free-text prompts.

## Consequences

**Easier**

- Users tune the Gemma 4 instruction (e.g. domain vocabulary, output format)
  without rebuilding, and switch between saved prompts instantly.
- The future language feature has one obvious integration point
  (`PromptTemplate.DefaultLanguage` + `Render`'s argument).

**Harder / trade-offs**

- A second JSON store (`prompts.json`) joins `manifest.json` and `settings.json`
  under the app data folder. Consistent with the existing registry pattern, but
  one more file to reason about.
- A malformed `{language}` (typo) silently won't substitute — it's a literal
  string replace, not a validated template. Acceptable for a single token.
- Built-in text now lives in `JsonPromptTemplateRegistry.BuiltInDefault` rather
  than in the recognizer; the recognizer no longer carries a fallback const, so
  it depends on the registry always returning a prompt (guaranteed by
  `GetActiveAsync`).

## Verification

- `dotnet build Parlotype.slnx -p:EnableCuda=false` — clean, zero warnings.
- New tests: `JsonPromptTemplateRegistryTests` (12 — built-in seeding,
  add/update/remove, built-in immutability, active fallback, `{language}`
  render, corrupt-file quarantine), `PromptSettingsViewModelTests` (8 — CRUD,
  built-in non-editable, select, duplicate). `SettingsWindowViewModelTests`
  updated to expect "Prompts" in the Gemma 4-active nav;
  `LlamaCppSpeechRecognizerPathResolutionTests` updated for the new ctor arg.
- Manual: select Gemma 4 → "Prompts" section appears (absent under Whisper);
  create/select a custom prompt; confirm via debug log
  (`prompt: {PromptName}`) that the chosen rendered text reaches llama-server.
