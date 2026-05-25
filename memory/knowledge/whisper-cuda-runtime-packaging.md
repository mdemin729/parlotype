---
title: Whisper.net CUDA runtime packaging
type: knowledge
tags: [cuda, whisper, packaging, release]
created: 2026-05-24
summary: Whisper.net.Runtime.Cuda ships only ggml-cuda-whisper.dll (~150 MB in published output) and relies on the user's installed CUDA toolkit for cudart/cublas — it does not bundle them
---

# Whisper.net CUDA runtime packaging

The `EnableCuda` MSBuild flag is named for the ~350 MB **NuGet package**
`Whisper.net.Runtime.Cuda`, but the marginal cost in a **published self-contained
`win-x64` output** is only ~150 MB.

Measured on 2026-05-24 (Whisper.net 1.9.0) for `dotnet publish -r win-x64
--self-contained true`:

- **Lite** (`EnableCuda=false`): ~720 MB unzipped. Contains Vulkan natives
  (`ggml-vulkan-whisper.dll`, `Avalonia.Vulkan.dll`) and **no** CUDA DLLs.
- **Full** (`EnableCuda=true`): ~870 MB unzipped. Adds **only** `ggml-cuda-whisper.dll`.

Key non-derivable quirk: the CUDA package does **not** bundle the CUDA runtime libraries
(`cudart64_*.dll`, `cublas64_*.dll`, `cublasLt64_*.dll`). The Full build therefore still
**requires the user to install the NVIDIA CUDA toolkit** (as the README prerequisites
state) — without it, the CUDA path fails to load and Parlotype falls back to Vulkan/CPU.

Implications:
- Both release variants are large because of the self-contained .NET runtime + the
  cross-platform Whisper natives, not because of CUDA. The Full-vs-Lite split saves
  ~150 MB, not ~350 MB.
- The "Full" zip alone is **not** a self-sufficient CUDA install — a CUDA-toolkit
  prerequisite note belongs anywhere we advertise the Full build (done in README
  Download/Releases section + ADR-031).
