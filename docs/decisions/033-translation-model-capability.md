---
status: accepted
date: 2026-05-25
---

# 033. Translation Capability per Whisper Model

## Context

Whisper's `translate` task (`WithTranslate()`) only produces useful output on models
trained for it. **English-only models** (`TinyEn`, `BaseEn`, `SmallEn`, `MediumEn`) are
trained on English audio only, and **Large v3 Turbo** is a distilled, transcription-only
model — none of them translate. ADR-021 added the translation toggle but explicitly
deferred enforcing model compatibility (see its "Harder" note).

In practice this caused real confusion: the user repeatedly enabled "Translating to
English", saw it silently do nothing, and only later realised the cause was the selected
model (Large v3 Turbo), not a bug.

## Decision

1. **`WhisperModelInfo` (Core) gains a `bool SupportsTranslation`** — the single source of
   truth. `false` for the four `*En` models and `LargeV3Turbo`; `true` for the remaining
   multilingual models.
2. **`AudioPipelineService` gates the effective flag**:
   `TranslateToEnglish = userIntent && WhisperModelInfo.Get(model).SupportsTranslation`.
   This is the authoritative enforcement point — translation can never reach an
   incompatible model regardless of UI state.
3. **User intent is preserved.** The `TranslateToEnglish` setting is *not* overwritten when
   an incompatible model is selected; only the *effective* value is gated. Switching back
   to a translation-capable model restores the user's previous choice.
4. **UI signals the constraint twice:**
   - The Whisper model list (`WhisperModelSettingsView`) shows a muted "no translation"
     hint next to models where `!SupportsTranslation`.
   - The translate toggle (`WhisperOutputSettingsViewModel.CanTranslate`) is disabled with
     an explanatory note when the selected model can't translate. `SettingsWindowViewModel`
     wires `WhisperModelSettingsViewModel.SelectedModel` changes to
     `WhisperOutputSettingsViewModel.UpdateTranslationAvailability(...)`, mirroring the
     existing engine-change subscription.
   - The note text (`TranslationUnavailableNote`) is **intent-aware** to avoid the apparent
     contradiction of a greyed-but-checked toggle: when the saved preference is enabled it
     reads "Translation is paused … resumes automatically when you pick a multilingual
     model" (surfacing the preserved choice); when disabled it reads "The selected model
     doesn't support translation."

## Consequences

### Easier
- The toggle can no longer silently fail; the user always sees why translation is off.
- Capability lives in one place (`WhisperModelInfo`) consumed by pipeline, model list, and toggle.

### Harder
- Two settings sections must stay reactively linked; the wiring lives in `SettingsWindowViewModel`.

### Risks
- The capability list is static metadata. If a future model's translation support differs,
  the `SupportsTranslation` flag must be updated alongside its catalog entry.
