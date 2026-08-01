---
status: accepted
date: 2026-07-31
---

# 050. Drop the Unused ONNX Runtime GPU Providers

## Context

Measuring the published output after [ADR-049](049-drop-whisper-cuda-runtime.md) turned up
a file larger than everything that ADR removed: `onnxruntime_providers_cuda.dll`, **391 MB**
— 54% of a 731 MB self-contained `win-x64` publish.

Its provenance was guessed wrong on first sighting (ADR-049 blamed `org.k2fsa.sherpa.onnx`).
The real chain is:

```
SileroVad 1.3.0
  └─ Microsoft.ML.OnnxRuntime.Gpu 1.18.1
       ├─ Microsoft.ML.OnnxRuntime.Gpu.Windows 1.18.1  → onnxruntime_providers_cuda.dll (391 MB)
       │                                                 onnxruntime_providers_tensorrt.dll
       └─ Microsoft.ML.OnnxRuntime.Gpu.Linux 1.18.1    → the lib*.so equivalents
```

Silero VAD needs ONNX Runtime; it does not need a GPU one. Three independent facts say the
providers are dead weight:

1. **Nothing registers them.** `SileroVadService` constructs `new Vad()` with default
   session options (CPU execution provider), and `ParakeetSpeechRecognizer` pins
   `config.ModelConfig.Provider = "cpu"` (ADR-041 — the pre-built sherpa-onnx package is
   CPU-only anyway).
2. **They could not load if asked.** They are built against ONNX Runtime 1.18.1, while the
   `onnxruntime.dll` that actually ends up in the output folder is sherpa-onnx's own newer
   native build, which overwrites the Microsoft one.
3. **Measured directly.** Running the Parakeet smoke benchmark with and without the DLL
   present in the output folder produces byte-identical metrics (WER 6.4%, CER 2.6%,
   RAM Δ 46.1 MB).

## Decision

Filter the ONNX Runtime GPU execution providers out of both build and publish output via a
new solution-wide `Directory.Build.targets`.

1. **Two targets** — `RemoveUnusedOnnxRuntimeProvidersFromBuild` (after `ResolveReferences`,
   filtering `ReferenceCopyLocalPaths`) and `RemoveUnusedOnnxRuntimeProvidersFromPublish`
   (after `ComputeResolvedFilesToPublishList`, filtering `ResolvedFileToPublish`). Both are
   needed: the first keeps `bin/` slim for every project including tests, the second is what
   the release artifact goes through.
2. **Match on filename substring** — `onnxruntime_providers_cuda` and
   `onnxruntime_providers_tensorrt`, which also covers the `lib*.so`/`.dylib` spellings and
   the accompanying `.lib` import libraries.
3. **Keep** `onnxruntime.dll`, `onnxruntime_providers_shared.dll` and the managed
   `Microsoft.ML.OnnxRuntime.dll` — required for any inference at all, GPU or not.
4. **Do not touch the dependency graph.** Swapping `Microsoft.ML.OnnxRuntime.Gpu` for the
   CPU package would mean overriding a transitive dependency of `SileroVad` and risking a
   different `onnxruntime.dll` in the folder that sherpa-onnx also writes to. Filtering the
   output leaves the working layout byte-identical minus the dead files.

## Consequences

- **Easier:** Published self-contained `win-x64` output drops from **731 MB to 338 MB** —
  a 54% cut, and more than double what ADR-049 saved. Every `bin/` in the solution loses
  391 MB too, which speeds up local rebuilds and clean checkouts.
- **Easier:** The download is now dominated by things that are actually used — the largest
  remaining files are Skia/HarfBuzz PDBs, the Vulkan Whisper natives, and the .NET runtime.
- **Harder:** If Parlotype ever wants a GPU execution provider for VAD or Parakeet, this
  filter has to be revisited — and the filenames are matched as literals, so an ONNX Runtime
  rename would silently stop filtering (a size regression, not a break).
- **Note:** The `Directory.Build.targets` applies to every project in the solution,
  including test projects. That is intentional; the VAD tests run real ONNX inference and so
  double as the regression guard for this change.
- **Note:** `libSkiaSharp.pdb` (80 MB) and `libHarfBuzzSharp.pdb` (20 MB) are now the two
  largest files. Excluding native PDBs from release publishes is a further ~100 MB, left for
  a separate change.
