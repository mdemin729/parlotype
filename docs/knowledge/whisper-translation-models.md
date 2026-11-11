---
title: "Whisper Translation Model Requirements"
area: "speech-engines"
summary: "Tiny/Base/Small can translate but poorly, Medium and larger are recommended, and the *En models and Large v3 Turbo cannot translate at all."
verified: 2026-11-11
---

# Whisper Translation Model Requirements

## Fact

Whisper's `translate` task (`.WithTranslate()` in Whisper.net) translates non-English speech to English text. Key constraints:

- **Translation-capable models**: `Tiny`, `Base`, `Small`, `Medium`, `LargeV1`, `LargeV2`, `LargeV3` (multilingual, non-turbo)
- **Models that do NOT support translation**:
  - English-only models `TinyEn`, `BaseEn`, `SmallEn`, `MediumEn` — trained on English audio only
  - **`LargeV3Turbo`** — a distilled, **transcription-only** model. Despite being multilingual for transcription, it was fine-tuned without the translation task and produces no useful translation.
- **`Tiny`, `Base`, `Small`** are multilingual but produce mixed-language or poor translations due to limited capacity. `Medium` or larger is recommended.
- Translation always outputs English — there is no way to translate to other target languages

## How It's Enforced

Capability lives in `WhisperModelInfo.SupportsTranslation` (Core, [ADR-033](../decisions/033-translation-model-capability.md)). `AudioPipelineService` gates the effective translate flag on it; the language UI (`LanguageRelationshipViewModel.WhisperModelSupportsTranslation`) shows the no-translation state. The old `TranslateToEnglish` setting is migrated by `LanguageSettingsMigrator` ([ADR-034](../decisions/034-source-target-language-selection.md), [ADR-036](../decisions/036-language-ux-rebuild.md)).

## Source

- [OpenAI Whisper documentation](https://github.com/openai/whisper) — large-v3-turbo is a pruned/distilled transcription model
- Empirical testing: translation toggle silently no-ops on `ggml-large-v3-turbo.bin`; works on `ggml-medium.bin`
