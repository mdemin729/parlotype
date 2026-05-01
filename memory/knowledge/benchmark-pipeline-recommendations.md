---
title: Benchmark-Derived Pipeline Recommendations
type: knowledge
tags: [whisper, benchmark, pipeline, vad, performance]
created: 2026-05-01
summary: Optimal STT settings from 234+ config benchmark sweep (ADR-011) — not derivable from code alone
---

# Benchmark-Derived Pipeline Recommendations

Source: [[decisions/_index|ADR-011]] — systematic sweep of 234+ configurations on LibriSpeech test-other (50 challenging samples) and smoke-test (3 clean samples).

## Optimal Settings

| Parameter | Best Accuracy | Best Speed |
|-----------|--------------|------------|
| Model | Medium | BaseEn |
| Language | `"en"` | `"en"` |
| Beam Size | 1 (greedy) | 1 (greedy) |
| Temperature | 0.0 | 0.0 |
| VAD | Disabled | Disabled |

## Non-Obvious Findings

> [!tip] Language Setting
> `language: "en"` gives **~2× speedup** vs `"auto"` with zero accuracy loss for English audio. This is free performance.

> [!tip] Beam Size
> Higher beam sizes (3, 5) **never improved** WER on any model tested. Greedy decoding (beam=1) is both fastest and most accurate.

> [!warning] English-Only Models
> English-only (`*En`) variants are **not universally better**. BaseEn excels, but SmallEn and MediumEn are worse than their multilingual counterparts on the benchmark datasets.

> [!warning] VAD for Batch
> VAD does not improve batch transcription accuracy — it marginally worsens WER by 0.2–0.5pp. VAD's value for real-time streaming is untested.

## Model Tier Quick Reference

| Priority | Model | WER (hard) | RTF | RAM |
|----------|-------|------------|-----|-----|
| Accuracy | Medium | ~9% | ~2.7 | ~2 GB |
| Balanced | Small | ~13% | ~0.9 | ~850 MB |
| Speed | BaseEn | ~15% | ~0.3 | ~400 MB |

## Current Gap

As of 2026-05-01, both V1 and V2 use the no-args `InitializeAsync()` which hardcodes `language: "auto"` and defaults to Base model — the recommended settings from this research are not applied. See `plans/2026-05-01-pipeline-settings-alignment/` for the implementation plan.
