---
status: accepted
date: 2026-04-28
---

# 014. NVIDIA/CUDA Environment Detection

## Context

ADR-012 introduced optional CUDA acceleration via `Whisper.net.Runtime.Cuda`, with silent CPU fallback when CUDA is unavailable. In practice, that silence makes CPU fallbacks very hard to diagnose: a user with a supported GPU may see slow transcription with no indication of what went wrong. The only signal Whisper.net 1.9.0 emits is a debug-level message routed through an internal log hook, and the message itself (e.g. `Cudart library couldn't be loaded`) does not distinguish between "no NVIDIA driver", "no CUDA toolkit", "wrong CUDA major version", and "stale process PATH".

A real session reproduced this: the IDE was launched before the CUDA 13.2 toolkit was installed, so it inherited a stale `PATH` and `cudart64_13.dll` could not be loaded — even though the driver, GPU, and toolkit were all present and correct. Whisper.net silently selected the CPU runtime. Restarting the IDE fixed it. Without first-party diagnostics, this class of issue is essentially invisible.

We also want the same data to feed a future settings/diagnostics UI, so users can see at a glance which driver and toolkits are installed and which `cudart` libraries are loadable from the application's process.

Relying on Whisper.net's internal probing for this is not viable: the NuGet build of `CudaHelper` differs from the upstream repository (in 1.9.0 it is hard-coded to `cudart64_13` only and does not surface version information), and any introspection we add today could break on a future Whisper.net upgrade.

## Decision

We will add a first-party NVIDIA/CUDA environment provider that runs at application startup and is independent of Whisper.net.

1. Define `INvidiaEnvironmentProvider` and `NvidiaEnvironmentInfo` (with a nested `CudaRuntimeProbe` record) in `Parlotype.Core.Speech`. The interface exposes `GetAsync` (cached) and `RefreshAsync` (force re-detection) so it can serve both startup logging and a future diagnostics UI.
2. Name it `Provider`, not `Reporter`, to reflect that it produces data for multiple consumers rather than emitting a one-shot report.
3. Implement `WindowsNvidiaEnvironmentProvider` in `Parlotype.Platform.Speech` using three independent, failure-isolated sources:
   - **`nvidia-smi`** parsing for driver version and the driver's max supported CUDA version.
   - **Filesystem scan** of `%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\v*` for installed toolkit versions.
   - **`cudart` P/Invoke probe** that calls `NativeLibrary.TryLoad` for known DLL names and, on success, calls `cudaRuntimeGetVersion` and `cudaDriverGetVersion` to capture both runtime and driver-reported CUDA versions.
4. Add `NoOpNvidiaEnvironmentProvider` returning `NvidiaEnvironmentInfo.Empty` for non-Windows platforms. DI selection is gated by `OperatingSystem.IsWindows()` in `PlatformServiceExtensions`.
5. Cache the first detection result; protect concurrent callers with `SemaphoreSlim`. `RefreshAsync` clears the cache and re-runs all sources.
6. Run detection from a fire-and-forget `Task.Run` invoked from `App.axaml.cs` immediately after `BuildServiceProvider`. Log a single Information-level line summarising driver, installed toolkits, and loadable runtimes.
7. Cover parsing helpers (`nvidia-smi` output, version path extraction, version-int decoding) with unit tests in `Parlotype.Tests`. The process-invocation seam is left for a future refactor when a UI consumer needs mockability.

## Consequences

- **Easier:** Users see at startup why CUDA was or wasn't selected — driver version, installed toolkits, and which `cudart` DLLs the process can actually load.
- **Easier:** A future settings/diagnostics view can consume the same provider without re-implementing detection.
- **Easier:** The detection survives Whisper.net upgrades because it does not depend on Whisper.net internals.
- **Easier:** Failures in any one source (missing `nvidia-smi`, no toolkits installed, no `cudart` DLL) are isolated and degrade gracefully to partial information instead of producing no information at all.
- **Harder:** Windows is the only platform supported initially. Linux and macOS will need their own implementations (parsing `nvidia-smi` plus checking `/usr/local/cuda*` is the natural starting point).
- **Harder:** A small amount of probing logic now exists in two places — Whisper.net's internal loader and our provider — but the duplication is intentional and the contracts are independent.
- **Harder:** The `nvidia-smi` invocation is not yet behind a mockable seam; tests cover only the pure parsing helpers. A `IProcessRunner` abstraction is the natural follow-up when a UI consumer drives that need.
