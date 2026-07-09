---
title: Parakeet fp32 model variant (user-selectable precision)
status: completed
created: 2026-07-08
started: 2026-07-08
completed: 2026-07-08
---

# Parakeet fp32 model variant

## Problem

Only the INT8 quantization of Parakeet TDT v3 was in the catalog. INT8 showed
a 16.7 % WER outlier on accented speech in the smoke set; users had no way to
trade disk/RAM for accuracy.

## Approach

Second `ParakeetModelInfo` catalog entry `parakeet-tdt-0.6b-v3-fp32`
(`csukuangfj/sherpa-onnx-nemo-parakeet-tdt-0.6b-v3`, ~2.6 GB). The existing
Settings → Parakeet model section (built in ADR-041, Whisper/Gemma pattern)
lists both entries automatically — select / download / delete already work.

Key detail: the fp32 encoder is a 42 MB graph plus a 2.44 GB **ONNX
external-data weights file** (`encoder.weights`). `ParakeetModelInfo` gained an
optional `EncoderWeightsFileName`; `FileNames` includes it, so the downloader,
cache check, and delete handle the fifth file. onnxruntime resolves the
weights by relative path — verified in the console spike and via benchmark.

Measured (smoke set, 16-core CPU): WER 1.9 % / CER 1.1 % / RTF 0.121 /
2.6 GB RAM vs INT8's 5.6 % / 2.5 % / 0.072 / 918 MB. INT8 stays the default.

## Workplan

- [x] Core: optional `EncoderWeightsFileName` on `ParakeetModelInfo` + `TdtV3Fp32` entry
- [x] Desktop: settings section description explains the INT8/fp32 trade-off
- [x] Spike-verify external-weights loading; benchmark fp32 vs INT8
- [x] Tests: catalog (5-file list, GetById, default-first), settings VM
      (fp32 select persists + unloads recognizer) — 737 green
- [x] Docs: ADR-041 amendment, vault (core profile, decisions index),
      knowledge note (external-data quirk)
