---
status: accepted
date: 2026-05-25
---

# 034. Source & Target Language Selection

## Context

Until now Parlotype transcribed with a hard-coded source language
(`Language = "auto"` in `AudioPipelineService.CacheSettingsAsync`) and could only
translate **to English** via Whisper's `TranslateToEnglish` toggle (ADR-021 / ADR-033).
Two capabilities were missing:

1. **Source-language selection** — pinning the spoken language improves recognition
   accuracy and is supported by both engines (Whisper's fixed ~99-language set; an LLM's
   open set).
2. **Arbitrary target-language translation** — translating the transcript into a language
   other than English. Whisper cannot do this (its `translate` task is English-only), but
   an LLM with audio input (Gemma 4 via llama.cpp) can, in a single call, by instruction.

The capabilities differ per engine, so the choices offered must be engine-aware.

## Decision

1. **Core language catalog (`LanguageCatalog`).** Two sources of truth:
   - `WhisperLanguages` — a curated static list of the ~99 codes Whisper recognises
     (the library does not expose this), including Whisper-specific codes (`yue`, `jw`, `haw`).
   - `AllLanguages` — derived at runtime from
     `CultureInfo.GetCultures(CultureTypes.NeutralCultures)`, the fallback "full list" for
     engines that don't declare a fixed set (LLMs).

   `LanguageInfo(Code, EnglishName, NativeName)` is the row type; native names come from
   `CultureInfo` when the code resolves, else fall back to the English name. Two sentinels:
   `AutoDetectCode = "auto"` and `NoTranslationCode = "none"`.

2. **Per-engine capabilities (`LanguageCapabilities` + `SpeechEngineCapabilities.For`).**
   Describes `SupportsAutoDetect`, the source-language set (`null` ⇒ full list), and
   `SupportsArbitraryTranslation`. Whisper → fixed set, no arbitrary translation (English
   translation stays on its existing toggle). Gemma 4 → full list, arbitrary translation.

3. **Recent-languages MRU (`RecentLanguages`).** Pure push-to-front / dedupe / cap-at-5
   helper; persisted as a `List<string>` under `SettingsKeys.RecentLanguages`. Sentinels
   and blanks are ignored.

4. **New settings keys:** `SelectedSourceLanguage` (default `"auto"`),
   `SelectedTargetLanguage` (default `"none"`), `RecentLanguages`.

5. **Pipeline wiring.**
   - *Whisper:* `AudioPipelineService.CacheSettingsAsync` reads `SelectedSourceLanguage`
     into `WhisperOptions.Language` (was hard-coded `"auto"`). Translation is unchanged —
     still the English-only `TranslateToEnglish` toggle gated by ADR-033.
   - *Gemma 4:* `LlamaCppSpeechRecognizer.BuildPromptTextAsync` renders the active prompt's
     `{language}` token with the source-language name, and — when a real target is selected
     — appends a single instruction to translate the transcript into the target language.
     Transcription and translation happen in one LLM call (no separate ASR step).

6. **Desktop UI.** A new `LanguageSelectionSettingsViewModel` / `LanguageSelectionSettingsView`
   section (category *Speech engine*, visible for both engines). It offers a source picker
   (Auto-detect + the engine's languages) always, and a target picker only when the active
   engine supports arbitrary translation. Recently used languages are pinned to the top of
   each picker. `SettingsWindowViewModel` calls `UpdateForEngine(...)` on engine change,
   mirroring the existing translate-availability wiring. For Whisper a hint points users to
   the "Translate to English" toggle under Whisper output.

## Consequences

### Easier
- Source language is now user-controllable, improving accuracy for known-language dictation.
- On-the-fly translation into arbitrary languages works today on the Gemma 4 engine.
- Language metadata lives in one Core catalog, consumed by pipeline and UI.

### Harder
- The curated Whisper language list is static; new Whisper language support must be added
  to `LanguageCatalog.WhisperLanguages`.
- Two settings sections (Language + Whisper output) jointly describe Whisper translation;
  the split is intentional (English-only toggle vs. source/arbitrary-target picker) but must
  be kept coherent.

### Risks / deferred
- **Whisper → LLM (ASR-less) pipeline** (Pipeline 2): translating Whisper output via a
  standalone LLM `ITextProcessor` is **not** implemented — documented as future work.
- **Keyboard-layout source detection** is deferred (platform-specific); only `auto` +
  explicit selection ship now.
- The Gemma translation instruction is prompt-based; output quality depends on the model
  honouring it. It is appended to the active prompt rather than replacing it.
