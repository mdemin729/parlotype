# CUDA GPU Support — Research Notes

## Whisper.net Runtime Architecture

Whisper.net uses a pluggable native runtime system. Each runtime ships as a separate NuGet package containing platform-specific native libraries:

| Package | Backend | Size | Platforms |
|---------|---------|------|-----------|
| `Whisper.net.Runtime` | CPU (GGML) | ~15 MB | win-x64, linux-x64, osx-arm64 |
| `Whisper.net.Runtime.Cuda` | NVIDIA CUDA 12 | ~350 MB | win-x64, linux-x64 |
| `Whisper.net.Runtime.CoreML` | Apple CoreML | ~15 MB | osx-arm64 |
| `Whisper.net.Runtime.Vulkan` | Vulkan (AMD/Intel) | ~30 MB | win-x64, linux-x64 |
| `Whisper.net.Runtime.OpenVino` | Intel OpenVINO | ~50 MB | win-x64, linux-x64 |
| `Whisper.net.AllRuntimes` | All of the above | ~450 MB | All |

## RuntimeOptions API

```csharp
namespace Whisper.net.LibraryLoader;

public static class RuntimeOptions
{
    // Must be set BEFORE first WhisperFactory creation
    public static List<RuntimeLibrary> RuntimeLibraryOrder { get; set; }

    // Read-only: reports which runtime was actually loaded
    public static RuntimeLibrary? LoadedLibrary { get; }

    // Optional: override native library search path
    public static string? LibraryPath { get; set; }
}

public enum RuntimeLibrary
{
    Cuda,
    Vulkan,
    CoreML,
    OpenVino,
    Cpu,
    CpuNoAvx
}
```

### Default order

```
Cuda → Vulkan → CoreML → OpenVino → Cpu → CpuNoAvx
```

### Critical constraint

From [GitHub issue #320](https://github.com/sandrohanea/whisper.net/issues/320):

> Once a runtime is loaded (i.e., after the first `WhisperFactory` is created), changing `RuntimeLibraryOrder` has no effect. The native library is loaded once per process lifetime.

This means:
1. We must configure `RuntimeOptions.RuntimeLibraryOrder` during app startup, before any Whisper work.
2. Changing GPU preference requires an application restart.
3. The setting must be read synchronously or with an early async await during DI registration.

## CUDA Detection

Whisper.net detects CUDA via `CudaHelper.IsCudaAvailable()`:
1. Tries to load `cudart64_12.dll` (Windows) or `libcuda.so` (Linux)
2. Calls `cudaGetDeviceCount()` to enumerate GPUs
3. Returns `true` if ≥1 GPU is found

If detection fails, the runtime probe moves to the next entry in `RuntimeLibraryOrder`.

## Performance expectations

From Whisper.net benchmarks and community reports:

| Model | CPU RTF | GPU (CUDA) RTF | Speedup |
|-------|---------|----------------|---------|
| Base | 0.3–0.5 | 0.05–0.1 | ~5× |
| Small | 0.8–1.0 | 0.1–0.2 | ~5× |
| Medium | 2.5–3.0 | 0.3–0.5 | ~6× |
| Large-v3 | 5.0–8.0 | 0.5–1.0 | ~8× |

RTF = Real-Time Factor (processing time / audio duration). Lower is better.

GPU acceleration is most impactful on larger models, making Medium and Large practical for real-time dictation.

## Current Parlotype pipeline measurements (CPU, ADR 011)

| Model | WER % | RTF | RAM (MB) |
|-------|-------|-----|----------|
| BaseEn | 14.9% | 0.26 | 411 |
| Small | 13.0% | 0.87 | 852 |
| Medium | 8.8% | 2.67 | 2032 |

With CUDA, Medium (best accuracy) would drop from RTF 2.67 to ~0.4, making it viable for interactive use.

## Deployment considerations

- **CUDA runtime requires NVIDIA drivers ≥ 528.33** (CUDA 12.0 minimum).
- The CUDA native libraries (~350 MB) ship inside the NuGet package and are copied to the output directory. No separate CUDA Toolkit install is needed by end users.
- CI runners without NVIDIA GPUs will still build and run — Whisper.net falls back to CPU transparently.
- The conditional `<EnableCuda>` build property keeps lightweight dev builds fast.
