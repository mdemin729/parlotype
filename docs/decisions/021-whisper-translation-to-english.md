---
status: accepted
date: 2026-05-03
---

# 021. Whisper Translation to English via Settings

## Context

Whisper supports translating non-English speech to English output via the `translate` task flag. The `WhisperSpeechRecognizer` already had a `WithTranslate()` call in the `InitializeAsync(WhisperOptions)` overload (used by benchmarks), but the desktop pipeline always used the no-args `InitializeAsync()` which built the processor with `WithLanguage("auto")` and no translation.

Users who speak non-English languages had no way to enable translation from the desktop app. Additionally, the recognizer's `IsReady` early-return guard prevented reinitialization when options changed — even if translation was later enabled, the already-loaded processor would not be rebuilt.

## Decision

1. **Add `TranslateToEnglish` to `WhisperOptions`** (Core) — a `bool` property, default `false`.
2. **Add `TranslateToEnglish` to `SettingsKeys`** (Core) — persisted via `JsonSettingsService`.
3. **`AudioPipelineService.CacheSettingsAsync()`** reads the translation setting (along with model type and runtime preference) and builds a `WhisperOptions` instance. `StartAsync()` passes this to `InitializeAsync(WhisperOptions)` instead of the no-args overload.
4. **`WhisperSpeechRecognizer.InitializeAsync(WhisperOptions)`** tracks `_currentOptions` and uses record value equality to detect changes. If called with different options while already initialized, it calls `UnloadAsync()` and reinitializes with the new configuration. Same options → fast early return.
5. **`SpeechSettingsViewModel`** exposes a `TranslateToEnglishEnabled` toggle, persisted to settings. The UI includes a note that translation works best with Medium or Large models.
6. **`WithTranslate()`** is now conditional on `options.TranslateToEnglish` rather than hardcoded.

## Consequences

### Easier
- Users can enable/disable English translation from Settings → Speech without code changes.
- The recognizer correctly reinitializes when options change between recordings — no app restart needed.
- `AudioPipelineService` now consistently uses `WhisperOptions`, eliminating the drift between no-args and options init paths.

### Harder
- Changing translation (or any Whisper option) mid-session triggers a full model reload (~1–2s with CUDA). This is acceptable since it only happens when options actually change.
- English-only models (`TinyEn`, `BaseEn`, etc.) do not support translation. The UI warns about model requirements but does not enforce the constraint. A future improvement could disable the toggle for `*En` models.

### Risks
- Translation quality depends on model size. `Base` and `Small` may produce mixed-language or poor translations. `Medium` and `Large` models are recommended.
