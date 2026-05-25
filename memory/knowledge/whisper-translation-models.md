---
title: Whisper Translation Model Requirements
type: knowledge
tags: [whisper, translation, models]
created: 2026-05-03
last_updated: 2026-05-25
summary:Whisper translation to English only works on multilingual non-turbo models (Medium, Large v1/v2/v3); English-only models (*En) AND Large v3 Turbo do NOT support it; Base/Small produce mixed results
---

# Whisper Translation Model Requirements

## Fact

Whisper's `translate` task (`.WithTranslate()` in Whisper.net) translates non-English speech to English text. Key constraints:

- **Translation-capable models**: `Tiny`, `Base`, `Small`, `Medium`, `LargeV1`, `LargeV2`, `LargeV3` (multilingual, non-turbo)
- **Models that do NOT support translation**:
  - English-only models `TinyEn`, `BaseEn`, `SmallEn`, `MediumEn` — trained on English audio only
  - **`LargeV3Turbo`** — a distilled, **transcription-only** model. Despite being multilingual for transcription, it was fine-tuned without the translation task and produces no useful translation. (Earlier versions of this note wrongly listed it as translation-capable; corrected 2026-05-25 after empirical confirmation by the user.)
- **`Tiny`, `Base`, `Small`** are multilingual but produce mixed-language or poor translations due to limited capacity. `Medium` or larger is recommended.
- Translation always outputs English — there is no way to translate to other target languages

## How It's Enforced (as of ADR-033)

Capability is encoded in `WhisperModelInfo.SupportsTranslation` (Core). `AudioPipelineService` gates the effective translate flag (`intent && SupportsTranslation`) so it never reaches an incompatible model; the saved `TranslateToEnglish` preference is preserved and restored when switching back to a capable model. The UI shows a "no translation" hint in the model list and disables the toggle (`WhisperOutputSettingsViewModel.CanTranslate`) with a note. See [[../decisions/_index|ADR-033]].

## Source

- [OpenAI Whisper documentation](https://github.com/openai/whisper) — large-v3-turbo is a pruned/distilled transcription model
- Empirical testing: translation toggle silently no-ops on `ggml-large-v3-turbo.bin`; works on `ggml-medium.bin`
