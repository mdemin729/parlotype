---
title: Whisper Translation Model Requirements
type: knowledge
tags: [whisper, translation, models]
created: 2026-05-03
last_updated: 2026-05-03
summary:Whisper translation to English only works reliably with multilingual models (Medium, Large); English-only models (*En) don't support it; Base/Small produce mixed results
---

# Whisper Translation Model Requirements

## Fact

Whisper's `translate` task (`.WithTranslate()` in Whisper.net) translates non-English speech to English text. Key constraints:

- **Only multilingual models support translation**: `Medium`, `LargeV1`, `LargeV2`, `LargeV3`, `LargeV3Turbo`
- **English-only models (`TinyEn`, `BaseEn`, `SmallEn`, `MediumEn`) do not support translation** — they are trained only on English audio
- **`Tiny`, `Base`, `Small`** are multilingual but produce mixed-language or poor translations due to limited capacity. `Medium` or larger is recommended.
- Translation always outputs English — there is no way to translate to other target languages

## Why This Matters

The `SpeechSettingsView` translation toggle does not currently enforce model compatibility. If a user enables translation with a `BaseEn` model, it will silently fail (transcribe English audio as English, or produce garbage for non-English audio). A future improvement should disable the toggle or warn when an incompatible model is selected.

## Source

- [OpenAI Whisper documentation](https://github.com/openai/whisper)
- Empirical testing with `ggml-large-v3-turbo.bin` (translation works) and `ggml-base.bin` (unreliable)
