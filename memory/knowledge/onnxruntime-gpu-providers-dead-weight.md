---
title: ONNX Runtime GPU providers are dead weight (SileroVad → OnnxRuntime.Gpu)
type: knowledge
tags: [onnxruntime, silero, vad, packaging, msbuild, size]
created: 2026-07-31
summary: SileroVad 1.3.0 depends on Microsoft.ML.OnnxRuntime.Gpu, which ships a 391 MB onnxruntime_providers_cuda.dll that Parlotype never loads — 54% of the published output until Directory.Build.targets filtered it out (ADR-050)
---

# ONNX Runtime GPU providers are dead weight

## The dependency nobody declares

`SileroVad 1.3.0` — the VAD package — depends on **`Microsoft.ML.OnnxRuntime.Gpu`**,
not the CPU package:

```
SileroVad 1.3.0
  └─ Microsoft.ML.OnnxRuntime.Gpu 1.18.1
       ├─ Microsoft.ML.OnnxRuntime.Gpu.Windows → onnxruntime_providers_cuda.dll (391 MB)
       │                                          onnxruntime_providers_tensorrt.dll
       └─ Microsoft.ML.OnnxRuntime.Gpu.Linux   → the lib*.so equivalents
```

Nothing in the repo names `Microsoft.ML.OnnxRuntime.Gpu`, so this is invisible until you
either read `obj/project.assets.json` or measure the publish output. `onnxruntime_providers_cuda.dll`
was **391 MB of a 731 MB** self-contained `win-x64` publish — bigger than anything the
Whisper CUDA removal ([[../decisions/_index|ADR-049]]) touched.

## Why it can never run

- `SileroVadService` builds `new Vad()` with default session options ⇒ CPU execution provider.
- `ParakeetSpeechRecognizer` pins `config.ModelConfig.Provider = "cpu"` (ADR-041).
- The providers are built against ORT **1.18.1**, but the `onnxruntime.dll` that actually
  lands in the folder is sherpa-onnx's own newer native build, which overwrites Microsoft's
  — see [[sherpa-onnx-quirks]]. Version-mismatched providers cannot load.

Verified empirically: running the Parakeet smoke benchmark with the DLL present vs absent
in the output folder gives byte-identical metrics (WER 6.4%, CER 2.6%, RAM Δ 46.1 MB).

## The fix, and why not the obvious one

`Directory.Build.targets` filters `ReferenceCopyLocalPaths` (build) and
`ResolvedFileToPublish` (publish) on the filename substrings `onnxruntime_providers_cuda`
and `onnxruntime_providers_tensorrt` — 731 MB → **338 MB** ([[../decisions/_index|ADR-050]]).

The tempting alternative — override the transitive dependency with the CPU package
`Microsoft.ML.OnnxRuntime` — was rejected: it changes which `onnxruntime.dll` competes with
sherpa-onnx's in the same folder, and that race currently resolves in a way that works.
Filtering output files leaves the working layout byte-identical minus the dead ones.

**Keep**: `onnxruntime.dll`, `onnxruntime_providers_shared.dll`, `Microsoft.ML.OnnxRuntime.dll`.
The first two are required for any inference, GPU or not.

## Generalisable lesson

A native NuGet package's *published* cost is not visible from the `.csproj`. When an
artifact looks unexpectedly large, sort the publish output by file size first — the
answer was a transitive dependency of a VAD library, not any of the speech engines that
the size was assumed to come from.
