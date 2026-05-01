# Pipeline Settings Research

## Source: ADR-011 — Optimal Speech-to-Text Pipeline Settings

234+ configurations benchmarked across two datasets:
- **smoke-test**: 3 clean speech samples
- **libri-speech-test-other**: 50 challenging samples (diverse speakers)

## Recommended Configurations

### Best Accuracy: Medium + greedy + English + no VAD

```json
{
  "whisper": { "model": "Medium", "language": "en", "beamSize": 1, "temperature": 0.0 },
  "vad": { "enabled": false }
}
```

### Best Speed: BaseEn + greedy + English + no VAD

```json
{
  "whisper": { "model": "BaseEn", "language": "en", "beamSize": 1, "temperature": 0.0 },
  "vad": { "enabled": false }
}
```

## Model Comparison (libri-speech-test-other, 50 samples)

| Model | WER % | CER % | RTF | RAM (MB) | Verdict |
|-------|-------|-------|-----|----------|---------|
| Base + auto (current default) | 16.2 | 6.4 | 0.47 | 408 | Previous default |
| **Medium + en** | **8.8** | **3.5** | **2.67** | **2032** | **Best accuracy** |
| Small + en | 13.0 | 5.8 | 0.87 | 852 | Balanced |
| **BaseEn + en** | **14.9** | **5.9** | **0.26** | **400** | **Best speed** |

## Model Comparison (smoke-test, 3 clean samples)

| Model | WER % | RTF | RAM (MB) |
|-------|-------|-----|----------|
| Tiny | 35.0 | 0.142 | 315 |
| TinyEn | 7.3 | 0.136 | 315 |
| Base | 3.6 | 0.276 | 409 |
| BaseEn | 0.9 | 0.276 | 411 |
| Small | 1.9 | 0.919 | 852 |
| SmallEn | 3.7 | 0.925 | 863 |
| Medium | 0.0 | 2.820 | 2047 |
| MediumEn | 3.7 | 2.916 | 2048 |

## Key Findings

### 1. Language Detection: `en` vs `auto`
Setting `language: "en"` provides **~2× speedup** with no accuracy loss for English audio. On BaseEn, RTF dropped from 0.28 to 0.155.

### 2. Beam Size
- `beamSize: 1` (greedy) is **optimal or tied for best** across all models tested
- Higher beam sizes (3, 5) either matched or **degraded** WER — never improved it
- Greedy is both fastest and most accurate

### 3. Temperature
- `temperature: 0.0` (deterministic) was **best or tied**
- Higher temperatures (0.2, 0.4) hurt accuracy on Base-class models

### 4. VAD Impact
- VAD **does not improve accuracy** — marginally worsened WER by 0.2–0.5pp
- Negligible speed benefit for batch processing
- May still help real-time streaming (not tested in benchmark)

### 5. English-Only Models
- BaseEn excels (best speed/accuracy ratio at its tier)
- SmallEn and MediumEn are **worse** than their multilingual counterparts
- English-only is not universally better

## Trade-off Guidance

| Priority | Model | WER (hard speech) | RTF | RAM |
|----------|-------|-------------------|-----|-----|
| Best accuracy | Medium | ~9% | ~2.7 | ~2 GB |
| Balanced | Small | ~13% | ~0.9 | ~850 MB |
| Best speed | BaseEn | ~15% | ~0.3 | ~400 MB |

## Current State vs Recommended (Gap Analysis)

| Parameter | Current Runtime | ADR-011 Recommended | Gap |
|-----------|----------------|---------------------|-----|
| Model | Base | Medium | ⚠️ 45% higher WER |
| Language | "auto" (hardcoded) | "en" | ⚠️ 2× slower |
| Beam Size | Whisper default | 1 (greedy) | Likely aligned, not guaranteed |
| Temperature | Whisper default | 0.0 | Likely aligned, not guaranteed |
| VAD | Always enabled | Disabled for batch | ⚠️ Streaming use case differs |

## V1 vs V2 Comparison

V1 and V2 share 100% identical pipeline settings — both use the same `AudioPipelineService` → `WhisperSpeechRecognizer.InitializeAsync()` (no-args) path. Neither app overrides any Core/Platform defaults.

## Future Work (from ADR-011)

- Test LargeV3 and LargeV3Turbo models
- Re-evaluate VAD impact on real-time streaming vs batch
- Test `initialPrompt` for domain-specific vocabulary hints
- Benchmark multilingual datasets
