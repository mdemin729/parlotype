---
title: "Session: 2026-05-22 — Configurable Gemma 4 prompts"
type: session
status: active
tags: [gemma4, prompts, settings, llamacpp]
created: 2026-05-22
summary: "Added a Settings page to create/edit/duplicate/delete/select Gemma 4 transcription prompts, backed by a JSON registry with a non-deletable built-in default and a {language} forward-compat placeholder."
---

# Session: 2026-05-22 — Configurable Gemma 4 prompts

## Active Focus
Replaced the hardcoded Gemma 4 transcription prompt with a user-managed,
selectable set of prompts (ADR-030).

- Core: `PromptTemplate` record (`{language}` token + `Render`),
  `IPromptTemplateRegistry`, `SettingsKeys.ActivePromptId`
  (`src/Parlotype.Core/Speech/`)
- Platform: `JsonPromptTemplateRegistry` (`prompts.json`, built-in seed merged
  at read time, corrupt-file quarantine), DI registration, and
  `LlamaCppSpeechRecognizer.TranscribeAsync` now resolves the active prompt per
  call (`src/Parlotype.Platform/Speech/`)
- Desktop: `PromptSettingsViewModel` + `PromptSettingsView` + `PromptDisplayItem`
  (SpeechEngine category, Gemma4-restricted), wired into `App.axaml.cs`,
  `SettingsWindowViewModel`, `SettingsWindow.axaml`
- Tests: `JsonPromptTemplateRegistryTests` (12), `PromptSettingsViewModelTests`
  (8), `MockPromptTemplateRegistry`; updated nav-ordering + recognizer-ctor tests

## Decisions Made
- Gemma 4 only — Whisper uses boolean toggles (ADR-021), no free-text prompt.
- Persist via a dedicated registry (`prompts.json`), not `ISettingsService`
  scalars — same split as managed llama-server installs (ADR-026). Only the
  active id lives in settings.
- Built-in default is non-deletable/non-editable, merged at read time → a
  working prompt always exists even if the file is deleted/hand-edited.
- Prompt resolved per `TranscribeAsync` call → change takes effect next
  utterance, no model reload (unlike model switch, ADR-017).
- `{language}` placeholder substituted with fixed `"English"` now; the shared
  keyboard-layout source-language setting is deferred (only the seam built).

## Facts Learned
- The original "Google-prescribed" prompt text now lives in
  `JsonPromptTemplateRegistry.BuiltInDefault`, not in the recognizer.
- `Parlotype.Tests` is xUnit v2 (no `TestContext`, no xUnit1051 enforcement);
  `Parlotype.Desktop.Tests` is xUnit v3 (must thread
  `TestContext.Current.CancellationToken`). Mirror the right pattern per project.
- (All derivable from code → no new `memory/knowledge/` entry.)

## Open Blockers
- None.

## Documentation Status
- ADR: done — `docs/decisions/030-configurable-gemma4-prompts.md`
- Vault (services): done — `core.md`, `platform.md`, `desktop.md`,
  `decisions/_index.md`
- Knowledge (non-derivable facts): none required
- Plan: `plans/2026-05-22-configurable-prompts/task.md` (completed)

## Next Action
Feature is complete, manually verified by the user, build clean, all 476 tests
pass. Candidate follow-up: the deferred **shared source-language setting** that
derives the language from the active keyboard layout and feeds both
`PromptTemplate.Render(language)` (Gemma) and Whisper's language param —
replace the `PromptTemplate.DefaultLanguage` const seam.
