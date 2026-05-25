---
title: Block translation for Whisper models that don't support it
status: completed
created: 2026-05-25
started: 2026-05-25
completed: 2026-05-25
---

# Block translation for Whisper models that don't support it

## Problem

Whisper's translation task only works on multilingual, non-turbo models. **Large v3 Turbo**
(distilled, transcription-only) and the **English-only models** (`*En`) do not translate.
The "Translating to English" toggle (Settings → Whisper output) could be enabled with any
model and silently did nothing on incompatible ones. The user repeatedly hit this: enabling
translation, seeing no effect, and only later realising the selected model was the cause.

## Approach

Encode translation capability as a single source of truth and enforce it where it matters,
while keeping the UI honest:

1. `WhisperModelInfo.SupportsTranslation` (Core) — capability flag per model.
2. `AudioPipelineService` gates the effective flag: `intent && SupportsTranslation` — so
   translation can never reach an incompatible model. The saved `TranslateToEnglish`
   preference is **preserved** (not overwritten) and restored on switching back.
3. UI signals the constraint twice: a "no translation" hint in the model list, and a
   disabled translate toggle (`WhisperOutputSettingsViewModel.CanTranslate`) with a note,
   wired through `SettingsWindowViewModel` reacting to model changes.

Scope (agreed with user): **all** non-translation models (`*En` + `LargeV3Turbo`).
Behaviour: **remember the user's choice** (gate the effective value, not the setting).

## Definition of Done

- [x] `dotnet build Parlotype.slnx` clean (no new warnings)
- [x] New + existing tests pass (`AudioPipelineTests`, `WhisperModelInfoTests`, `WhisperOutputSettingsViewModelTests`)
- [ ] Manual run: Turbo/`*En` show hint; toggle disables + note; preference restored on switch back
- [x] ADR-033 written; `memory/decisions/_index.md`, `core.md`, `desktop.md`, knowledge note updated

See [implementation-plan.md](implementation-plan.md) for the file-by-file plan.
