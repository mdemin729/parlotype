---
status: accepted
date: 2026-03-15
---

# 011. Optimal Speech-to-Text Pipeline Settings

## Context

Parlotype's speech-to-text pipeline has multiple tunable parameters: Whisper model size, beam search size, temperature, language detection mode, and VAD (Voice Activity Detection) preprocessing settings. We needed to determine the optimal configuration that maximizes transcription accuracy (lowest WER) across diverse speech conditions.

A systematic benchmark sweep was conducted across 234+ configurations using two datasets:
- **smoke-test**: 3 samples (clean speech, accented speech with pauses)
- **libri-speech-test-other**: 50 samples from LibriSpeech test-other (challenging speech with diverse speakers)

## Decision

### Recommended Configuration: **Medium model, greedy decoding, English, no VAD**

```json
{
  "whisper": {
    "model": "Medium",
    "language": "en",
    "beamSize": 1,
    "temperature": 0.0
  },
  "vad": {
    "enabled": false
  }
}
```

### Alternative (speed-optimized): **BaseEn model, greedy decoding, English, no VAD**

```json
{
  "whisper": {
    "model": "BaseEn",
    "language": "en",
    "beamSize": 1,
    "temperature": 0.0
  },
  "vad": {
    "enabled": false
  }
}
```

### Key Findings

#### Model Comparison (libri-speech-test-other, 50 samples)

| Model  | WER %  | CER %  | RTF   | RAM (MB) | Verdict |
|--------|--------|--------|-------|----------|---------|
| Base (baseline, auto) | 16.2 | 6.4 | 0.469 | 408 | Previous default |
| **Medium (en)** | **8.8** | **3.5** | **2.67** | **2032** | **Best accuracy** |
| Small (en) | 13.0 | 5.8 | 0.869 | 2061 | Mid-tier |
| **BaseEn (en)** | **14.9** | **5.9** | **0.260** | **2061** | **Best speed** |

#### Language Detection: `en` vs `auto`

Setting `language: "en"` instead of `"auto"` provides a consistent **~2× speedup** with no accuracy loss for English audio. On BaseEn model, RTF dropped from 0.28 to 0.155.

#### Beam Size & Temperature

- **beam=1 (greedy)** is optimal or tied for best across all models
- Higher beam sizes (3, 5) either matched or degraded WER — they never improved it
- **temp=0.0** (deterministic) was best or tied; higher temperatures (0.2, 0.4) hurt accuracy on Base-class models

#### VAD Impact

- VAD **does not improve accuracy** on any model tested — it marginally worsened WER by 0.2–0.5pp
- VAD provides negligible speed benefit when processing full audio clips
- VAD may still be valuable for **real-time streaming** (incremental processing, silence detection) but is not beneficial for batch transcription accuracy

#### Model Size vs Accuracy (smoke-test, 3 samples)

| Model    | WER % | RTF   | RAM (MB) |
|----------|-------|-------|----------|
| Tiny     | 35.0  | 0.142 | 315      |
| TinyEn   | 7.3   | 0.136 | 315      |
| Base     | 3.6   | 0.276 | 409      |
| BaseEn   | 0.9   | 0.276 | 411      |
| Small    | 1.9   | 0.919 | 852      |
| SmallEn  | 3.7   | 0.925 | 863      |
| Medium   | 0.0   | 2.820 | 2047     |
| MediumEn | 3.7   | 2.916 | 2048     |

Notable: English-only (`*En`) models are not universally better — BaseEn excels, but SmallEn and MediumEn are *worse* than their multilingual counterparts.

## Consequences

### Positive
- **45% WER reduction** on challenging speech (16.2% → 8.8%) by switching from Base/auto to Medium/en
- Clear, data-driven default configuration for the application
- `language: "en"` provides free 2× speedup for English-only use cases
- Greedy decoding (beam=1) is both fastest and most accurate — simplest implementation

### Negative
- Medium model requires ~1.5 GB download and ~2 GB RAM vs ~150 MB for Base
- RTF of 2.67 means transcription takes ~2.7× longer than real-time (vs 0.26 for BaseEn)
- Users with limited RAM or who prioritize speed should be offered BaseEn as an alternative

### Trade-off Guidance

| Priority | Model | Expected WER (hard speech) | RTF | RAM |
|----------|-------|---------------------------|-----|-----|
| Best accuracy | Medium | ~9% | ~2.7 | ~2 GB |
| Balanced | Small | ~13% | ~0.9 | ~850 MB |
| Best speed | BaseEn | ~15% | ~0.3 | ~400 MB |

### Future Work
- Test LargeV3 and LargeV3Turbo models for potential accuracy gains
- Re-evaluate VAD impact on real-time streaming scenarios (vs batch)
- Test `initialPrompt` parameter for domain-specific vocabulary hints
- Benchmark on multilingual datasets if non-English support is needed
