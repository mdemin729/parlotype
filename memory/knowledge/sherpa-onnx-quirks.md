---
title: sherpa-onnx Quirks
type: knowledge
tags: [sherpa-onnx, parakeet, onnx, gotchas]
created: 2026-07-07
last_updated: 2026-07-07
summary: Non-obvious behaviours of the org.k2fsa.sherpa.onnx 1.13.3 NuGet package used by the Parakeet engine
---

# sherpa-onnx 1.13.3 Quirks

Facts learned during the Parakeet TDT v3 spike ([[decisions/_index|ADR-041]],
`plans/2026-07-06-parakeet-v3-engine/`) that are not derivable from our code.

## 1. Pre-built NuGet is CPU-only

`org.k2fsa.sherpa.onnx` pulls per-RID native runtime packages
(`org.k2fsa.sherpa.onnx.runtime.win-x64` etc.) that bundle their **own**
onnxruntime — CPU execution provider only. CUDA requires building sherpa-onnx
from source with `-DSHERPA_ONNX_ENABLE_GPU=ON` and CUDA 11.8/12.x
(github.com/k2-fsa/sherpa-onnx issues #1044, #1313). Setting
`config.ModelConfig.Provider = "cuda"` on the stock package does not work.

The sherpa native package also **wins the `onnxruntime.dll` race**: it drops its
own newer build (16 MB, ORT 1.2x) into the output folder, overwriting the 1.18.1
one that `Microsoft.ML.OnnxRuntime.Gpu` puts there via SileroVad. Worth knowing
before reasoning about which ONNX Runtime is actually loaded at runtime — see
[[onnxruntime-gpu-providers-dead-weight]].

## 2. Config objects use public fields, not properties

`OfflineRecognizerConfig`, `OfflineModelConfig`, `OfflineTransducerModelConfig`
expose public **fields** (`config.ModelConfig.Tokens = …`). Object initializers
work, but reflection-based tooling (serializers, mappers) that only looks at
properties will see nothing.

## 3. No confidence / language fields for NeMo transducers

`OfflineRecognizerResult` exposes `Text`, `Tokens`, `Timestamps`, `Durations`
only — no confidence and no detected-language field in the C# wrapper (the
`lang` slot exists in the C API for SenseVoice-style models only). Hence
`TranscriptionResult.Confidence`/`DetectedLanguage` are null for Parakeet.

## 4. `AcceptWaveform` resamples internally

Feeding a sample rate ≠ 16 kHz logs `Creating a resampler` **to stderr** (red
in consoles) and resamples transparently — not an error. Native-library log
output goes to stderr, not through our ZLogger bridge.

## 5. fp32 export uses ONNX external data — loads transparently

The full-precision export is a small graph (`encoder.onnx`, 42 MB) plus a
separate `encoder.weights` (2.44 GB) external-data file. sherpa-onnx /
onnxruntime resolve it **by relative path next to the graph file** with no
extra config — but that means the weights file must be downloaded into the
same directory, and deleting a cached model must remove it too.

## 6. Measured behaviour (Parakeet TDT 0.6B v3, 16-core CPU)

- INT8: load ~2.9–3.3 s, ~715 MB working set, decode RTF ≈ 0.07–0.13
- fp32: load ~5.8–6.1 s, ~2.2–2.6 GB working set, decode RTF ≈ 0.12–0.18;
  smoke-set WER 1.9 % vs INT8's 5.6 % (accented speech drives the gap)
- `NumThreads = ProcessorCount / 2`
- Output includes punctuation + capitalization natively
- `OfflineRecognizer` construction and `Decode` are synchronous — wrap in
  `Task.Run` (same lesson as [[whisper-ui-thread-loading]])
