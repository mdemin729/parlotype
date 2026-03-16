---
status: accepted
date: 2026-03-16
---

# 012. CUDA GPU Acceleration

## Context

Parlotype currently runs Whisper inference on CPU only via `Whisper.net.Runtime`. That works for smaller models, but larger models such as Medium and Large currently have real-time factors above 2.5×, which is too slow for interactive dictation.

NVIDIA CUDA can reduce inference time by roughly 3-5× on supported hardware, making larger Whisper models much more practical. However, Whisper.net does not automatically select CUDA at runtime without explicit configuration. Instead, it uses a pluggable runtime system controlled through `RuntimeOptions.RuntimeLibraryOrder`.

A key constraint is that `RuntimeOptions.RuntimeLibraryOrder` must be configured before the first `WhisperFactory` is created. The setting is process-global and effectively one-shot for the lifetime of the process, so changing GPU preference after initialization requires an application restart.

## Decision

We will support optional CUDA acceleration by configuring Whisper.net runtime selection at application startup, while preserving a reliable CPU fallback.

1. Add `Whisper.net.Runtime.Cuda` v1.9.0 as a conditional NuGet reference behind the `EnableCuda` build property, which defaults to `true`.
2. Add a `RuntimePreference` enum in `Parlotype.Core` with `Auto` and `Cpu` values so runtime selection can be expressed as an application-level preference.
3. Add a static `WhisperRuntimeBootstrap` helper that configures `RuntimeOptions.RuntimeLibraryOrder` before the first `WhisperFactory` is created.
4. In `Auto` mode, try CUDA first and fall back to CPU silently if CUDA is unavailable. In `Cpu` mode, force CPU-only runtime selection.
5. Make the bootstrap idempotent and thread-safe with first-call-wins semantics, and invoke it lazily from `WhisperSpeechRecognizer.InitializeAsync`.
6. Expose a `--gpu` flag in the benchmark CLI for explicit runtime control during performance testing.
7. Record the loaded runtime in `EnvironmentInfo` so benchmark results show whether a run used GPU or CPU.

## Consequences

- **Easier:** Users with supported NVIDIA GPUs get automatic acceleration, making larger Whisper models viable for interactive use.
- **Easier:** Benchmarks can compare GPU and CPU performance explicitly by running with `--gpu false`.
- **Easier:** CPU-only builds remain possible with `-p:EnableCuda=false`, which also keeps CI faster and lighter.
- **Harder:** GPU preference changes require an application restart because runtime selection is process-global and fixed before the first `WhisperFactory` is created.
- **Harder:** The CUDA NuGet package adds roughly 350 MB to build output, though this is mitigated by the `EnableCuda` build property.
- **Harder:** Future accelerated runtimes such as CoreML or Vulkan will need similar runtime bootstrap logic, but with separate configuration and packaging decisions.
