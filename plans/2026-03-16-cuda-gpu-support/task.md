---
title: NVIDIA GPU (CUDA) support for STT pipeline
status: planned
created: 2026-03-16
started:
completed:
---

# NVIDIA GPU (CUDA) Support for STT Pipeline

## Problem

Parlotype currently runs all Whisper inference on the CPU via the `Whisper.net.Runtime` package. On larger models (Medium, Large) the real-time factor (RTF) exceeds 2.5×, making them impractical for interactive dictation. NVIDIA GPUs with CUDA can reduce inference time by 3–5× but the pipeline has no GPU awareness today.

### Current state

| Layer | File | What it does |
|-------|------|--------------|
| NuGet refs | `Parlotype.Platform.csproj` | `Whisper.net` + `Whisper.net.Runtime` v1.9.0 (CPU only) |
| Factory init | `WhisperSpeechRecognizer.cs:42` | `WhisperFactory.FromPath(modelPath)` — no `RuntimeOptions` |
| Options | `WhisperOptions.cs` | 6 properties — none for GPU/runtime selection |
| Settings keys | `SettingsKeys.cs` | 6 keys — no GPU preference |
| DI | `PlatformServiceExtensions.cs` | Registers `WhisperSpeechRecognizer` as singleton |

## Approach

Use Whisper.net's built-in runtime loading system. The library probes runtimes in the order defined by `RuntimeOptions.RuntimeLibraryOrder` (a static property) and loads the first one that succeeds. CUDA detection uses `CudaHelper.IsCudaAvailable()` internally.

### Key API constraint

`RuntimeOptions.RuntimeLibraryOrder` **must be set before the first `WhisperFactory` is created**. Once a native runtime is loaded, it cannot be changed without restarting the process. This means:

- The preferred runtime must be read from settings during service registration, before any Whisper work begins.
- Changing the GPU preference at runtime requires an app restart (surface this in UX).

### Design decisions

1. **CUDA-only scope** — this plan covers NVIDIA CUDA. CoreML (macOS) and Vulkan (AMD/Intel) are future work.
2. **Conditional NuGet reference** — `Whisper.net.Runtime.Cuda` is ~350 MB. Include it as an optional package via a build property so CI and lightweight dev builds can skip it.
3. **Graceful fallback** — if CUDA is unavailable at runtime (no GPU, no drivers), fall back to CPU silently with a log warning. Never crash.
4. **No UI in this plan** — settings toggle is a follow-up. This plan wires the infrastructure and exposes it through `WhisperOptions` and `SettingsKeys`. Benchmark CLI gets a `--gpu` flag for immediate testing.

### Runtime selection strategy

```
Order: [Cuda, Cpu]   — when user preference is "gpu" (default if CUDA package present)
Order: [Cpu]         — when user preference is "cpu-only"
```

The loaded runtime is logged at startup. `RuntimeOptions.LoadedLibrary` reports which backend was actually loaded.

## Workplan

### Phase 1 — Core infrastructure

- [ ] **P1.1** Add `Whisper.net.Runtime.Cuda` v1.9.0 to `Parlotype.Platform.csproj` behind a build property `<EnableCuda>true</EnableCuda>` (default true). The reference uses `Condition="'$(EnableCuda)' != 'false'"` so builds without CUDA are possible.
- [ ] **P1.2** Add a `RuntimePreference` enum to `Parlotype.Core/Speech/` with values `Auto`, `Cpu`. `Auto` means try GPU first, then CPU.
- [ ] **P1.3** Extend `WhisperOptions` with `RuntimePreference RuntimePreference { get; init; } = RuntimePreference.Auto`.
- [ ] **P1.4** Add `SettingsKeys.RuntimePreference = "RuntimePreference"`.

### Phase 2 — Recognizer integration

- [ ] **P2.1** Create `Parlotype.Platform/Speech/WhisperRuntimeBootstrap.cs` — a static helper called once at startup:
  - Reads the `RuntimePreference` setting.
  - Sets `RuntimeOptions.RuntimeLibraryOrder` accordingly.
  - Logs the configured order and, after factory creation, the actually loaded runtime via `RuntimeOptions.LoadedLibrary`.
  - Exposes `RuntimeLibrary? LoadedRuntime` for downstream consumers.
- [ ] **P2.2** Update `WhisperSpeechRecognizer.InitializeAsync` (both overloads) to:
  - Call `WhisperRuntimeBootstrap.EnsureInitialized(settings, logger)` before creating `WhisperFactory`.
  - Log `RuntimeOptions.LoadedLibrary` after factory creation.
  - Pass `useGpu: true` to `WhisperProcessorBuilder` if CUDA is loaded (Whisper.net maps this to GPU device 0).
- [ ] **P2.3** Update `PlatformServiceExtensions.AddPlatformServices` to eagerly call `WhisperRuntimeBootstrap.Initialize()` during DI setup (reads setting, sets `RuntimeOptions`). This ensures the static is configured before any consumer resolves.

### Phase 3 — Benchmark CLI

- [ ] **P3.1** Add `--gpu` / `--no-gpu` option to the benchmark `run` command. Maps to `RuntimePreference.Auto` / `RuntimePreference.Cpu`.
- [ ] **P3.2** Add `GpuAcceleration` field to benchmark config JSON schema (`WhisperConfig` section).
- [ ] **P3.3** Record the loaded runtime (`Cuda` vs `Cpu`) in benchmark result JSON for each run.
- [ ] **P3.4** Include loaded runtime in benchmark `list` and `compare` output.

### Phase 4 — Testing

- [ ] **P4.1** Unit test `WhisperRuntimeBootstrap` logic: verify it maps `RuntimePreference.Auto` → `[Cuda, Cpu]` and `RuntimePreference.Cpu` → `[Cpu]`.
- [ ] **P4.2** Unit test `WhisperOptions` defaults: `RuntimePreference` should be `Auto`.
- [ ] **P4.3** Integration-style test: create `WhisperFactory` after bootstrap and verify `RuntimeOptions.LoadedLibrary` is not null (runs on CI with CPU — verifies fallback).
- [ ] **P4.4** Benchmark test: verify `--gpu` / `--no-gpu` flags parse correctly.

### Phase 5 — Documentation & ADR

- [ ] **P5.1** Create ADR `012-cuda-gpu-acceleration.md` capturing: runtime selection strategy, static `RuntimeOptions` constraint, conditional NuGet reference pattern, fallback behavior.
- [ ] **P5.2** Update `CLAUDE.md` with CUDA build notes (e.g., `dotnet build -p:EnableCuda=false` for CPU-only builds).

## Out of scope

- Desktop UI toggle for GPU preference (follow-up plan).
- CoreML / Vulkan / OpenVINO runtimes.
- Multi-GPU device selection (Whisper.net uses device 0).
- GPU memory monitoring or limits (Whisper.net manages this internally).

## Risks

| Risk | Mitigation |
|------|------------|
| `Whisper.net.Runtime.Cuda` adds ~350 MB to build output | Conditional reference via `EnableCuda` property; CI can build without |
| CUDA requires matching driver version | Whisper.net handles detection; we fall back to CPU and log a warning |
| `RuntimeOptions` is process-global and one-shot | Bootstrap early in DI setup; document restart requirement for preference change |
| CUDA runtime may not load in test environments | Tests verify fallback path; GPU-specific tests gated by `[Trait("GPU", "true")]` |

## References

- [Whisper.net GitHub](https://github.com/sandrohanea/whisper.net)
- [Whisper.net.Runtime.Cuda NuGet](https://www.nuget.org/packages/Whisper.net.Runtime.Cuda)
- [RuntimeOptions source](https://github.com/sandrohanea/whisper.net/blob/main/Whisper.net/LibraryLoader/RuntimeOptions.cs)
- [RuntimeOptions one-shot constraint](https://github.com/sandrohanea/whisper.net/issues/320)
- Internal: `docs/research/voice-to-text-research.docx.md` (GPU acceleration section)
