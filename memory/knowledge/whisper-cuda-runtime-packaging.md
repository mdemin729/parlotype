---
title: Whisper.net CUDA runtime packaging (historical)
type: knowledge
tags: [cuda, whisper, packaging, release, historical]
created: 2026-05-24
updated: 2026-07-31
summary: Why the CUDA runtime was worth less than its name suggested — it added only ~150 MB of published output and bundled no cudart/cublas, so the Full build still needed the user's CUDA toolkit. Removed in ADR-049; kept as the rationale trail.
---

# Whisper.net CUDA runtime packaging (historical)

> **Historical since [[../decisions/_index|ADR-049]] (2026-07-31).** `Whisper.net.Runtime.Cuda`,
> the `EnableCuda` flag and the Full/Lite release split no longer exist. Kept because the
> measurements below are the evidence that motivated the removal.

The `EnableCuda` MSBuild flag was named for the ~350 MB **NuGet package**
`Whisper.net.Runtime.Cuda`, but the marginal cost in a **published self-contained
`win-x64` output** was only ~150 MB.

Measured on 2026-05-24 (Whisper.net 1.9.0) for `dotnet publish -r win-x64
--self-contained true`:

- **Lite** (`EnableCuda=false`): ~720 MB unzipped. Vulkan natives, no CUDA DLLs.
- **Full** (`EnableCuda=true`): ~870 MB unzipped. Added **only** `ggml-cuda-whisper.dll`.

Key non-derivable quirk: the CUDA package did **not** bundle the CUDA runtime libraries
(`cudart64_*.dll`, `cublas64_*.dll`, `cublasLt64_*.dll`). The Full build therefore still
**required the user to install the NVIDIA CUDA toolkit** — without it the CUDA path failed
to load and Parlotype fell back to Vulkan/CPU. A "Full" download was never a self-sufficient
CUDA install, which is why so much of the Runtime settings page was toolkit guidance.

Post-removal measurement (2026-07-31, same publish command): **731 MB**, one artifact.

The dominant remaining cost is *not* Whisper: `onnxruntime_providers_cuda.dll` from
`org.k2fsa.sherpa.onnx` is **391 MB** of that output and is never loaded, because
`ParakeetSpeechRecognizer` sets `Provider = "cpu"` ([[../decisions/_index|ADR-041]]).
See [[sherpa-onnx-quirks]].
