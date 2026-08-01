---
status: accepted
amended-by: 049
date: 2026-05-05
---

> **Amended by [ADR-049](049-drop-whisper-cuda-runtime.md) (2026-07-31).** Vulkan is now the
> *only* GPU runtime: `RuntimePreference` is `Auto / Vulkan / Cpu` and `Auto` chains
> Vulkan -> CPU.

# 022. Vulkan GPU Acceleration

## Context

ADR-012 introduced optional CUDA acceleration for Whisper inference, but only NVIDIA hardware benefits. Users on AMD, Intel Arc, integrated GPUs, or non-Windows hosts in the future have no GPU path and run on CPU only — which is impractical for the Medium and Large Whisper models.

Whisper.net 1.9 ships a `Whisper.net.Runtime.Vulkan` package that builds against the Vulkan API. Vulkan is supported on virtually any modern discrete GPU, most integrated GPUs, and across Windows / Linux / Android (and macOS via MoltenVK in the future). The same `RuntimeOptions.RuntimeLibraryOrder` mechanism used by CUDA also supports a `RuntimeLibrary.Vulkan` value, so the bootstrap pattern from ADR-012 generalises naturally.

The key design tension is the `Auto`-vs-strict tradeoff. Whisper.net's order-based selection silently falls back from one entry to the next, which is exactly what users want when they ask for "GPU acceleration". But it's the wrong behaviour when a user has explicitly chosen "use CUDA" or "use Vulkan" — silent CPU fallback hides driver/install problems instead of surfacing them.

## Decision

We will support optional Vulkan acceleration alongside CUDA, with explicit per-runtime selection in the UI and strict (no-fallback) semantics for the explicit choices.

1. Add `Whisper.net.Runtime.Vulkan` v1.9.0 as an **unconditional** package reference. The Vulkan native libraries are roughly 30 MB — small enough not to need an `EnableVulkan` build flag analogous to `EnableCuda`.
2. Extend `RuntimePreference` from `{ Auto, Cpu }` to `{ Auto, Cuda, Vulkan, Cpu }`. `Auto` becomes a chained probe: CUDA → Vulkan → CPU. `Cuda` and `Vulkan` are **strict** — no fallback.
3. Add a new `RuntimeUnavailableException` in `Parlotype.Core`. `WhisperSpeechRecognizer` performs a pre-flight environment check (using `INvidiaEnvironmentProvider` / `IVulkanEnvironmentProvider`) and throws this exception when the requested strict runtime is not detected, instead of silently falling back. `TranscribeViewModel` catches it and surfaces a status-bar error directing the user to Settings.
4. Add `IVulkanEnvironmentProvider` + `VulkanEnvironmentInfo` in `Parlotype.Core` and a Windows implementation `WindowsVulkanEnvironmentProvider` in `Parlotype.Platform`. The Windows provider probes `vulkan-1.dll` via `NativeLibrary.TryLoad`, queries the loader API version with `vkEnumerateInstanceVersion`, enumerates physical devices via `vkCreateInstance` + `vkEnumeratePhysicalDevices`, and reads the `VULKAN_SDK` env var. A `NoOpVulkanEnvironmentProvider` is registered on non-Windows platforms.
5. Surface a new **Settings → Runtime** section that mirrors the Whisper Model selector. Items for CUDA / Vulkan are dimmed when the corresponding environment isn't detected, with an inline reason and a link to https://vulkan.lunarg.com/sdk/home when the Vulkan loader is missing. Selection is persisted under the existing `SettingsKeys.RuntimePreference` key. The view shows a "Changes take effect after restart" notice — runtime selection is process-global and one-shot (the same constraint as ADR-012).
6. Log Vulkan environment info at app startup, parallel to the existing NVIDIA log (loader version, SDK presence, detected devices).

## Consequences

- **Easier:** Users on AMD, Intel, and other non-NVIDIA GPUs get GPU acceleration without driver wrangling — `vulkan-1.dll` ships with most modern GPU drivers.
- **Easier:** Strict `Cuda` / `Vulkan` modes give power users a way to pin behaviour and surface misconfiguration instead of silently degrading. The Auto path keeps the friendly "just works" semantics for everyone else.
- **Easier:** `Auto` becomes more useful — Vulkan widens the GPU-eligible population significantly while still preferring CUDA on NVIDIA hardware.
- **Harder:** Build output grows by ~30 MB (Vulkan native libs). Acceptable trade-off; smaller than the CUDA package and not gated by a build flag.
- **Harder:** Runtime selection still requires an application restart (same as CUDA — process-global one-shot constraint).
- **Harder:** Vulkan device enumeration requires P/Invoke into `vulkan-1.dll`. Failures are absorbed (the provider just reports an empty device list) but the surface area is non-trivial. Future macOS support will need MoltenVK consideration.
- **Harder:** The `WhisperSpeechRecognizer` constructor now takes both environment providers, breaking direct `new(...)` construction in tests. Acceptable — the providers are lightweight and the no-op variants are easy to drop in.
