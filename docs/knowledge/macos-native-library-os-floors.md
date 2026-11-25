---
title: "macOS version floors of the bundled native libraries"
area: "macos"
summary: "sherpa-onnx's ONNX Runtime needs macOS 14.4, Whisper.net declares 15.0 but loads on 13, .NET 10 supports 15+ but runs from 12, and three publish filters written for Windows break a macOS publish."
verified: 2026-11-14
---

# macOS version floors of the bundled native libraries

Measured on the packages Parlotype pins, on an Intel Mac running macOS 13.7
([ADR-078](../decisions/078-macos-support.md)). Re-measure when a package is bumped.

## What each library needs

| Library | Package | Declares (`minos`) | Actually needs |
|---|---|---|---|
| `libonnxruntime.1.24.4.dylib` (x64) | `org.k2fsa.sherpa.onnx.runtime.osx-x64` 1.13.3 | 15.5 | **14.4**: imports `_OBJC_CLASS_$_MLComputePlan` from CoreML, which does not exist earlier. Fails at load, not at use. |
| `libonnxruntime.1.24.4.dylib` (arm64) | `…osx-arm64` 1.13.3 | 14.0 | same symbol, so 14.4 in practice |
| `libsherpa-onnx-c-api.dylib` | same | 10.15 | whatever its ONNX Runtime needs; it links `@rpath/libonnxruntime.1.24.4.dylib` by that exact name |
| `libwhisper.dylib`, `libggml-*-whisper.dylib` | `Whisper.net.Runtime` 1.9.0 | 15.0 | loads and transcribes on 13.7 (x64). Not tried on arm64 below 15. |
| `libonnxruntime.dylib` 1.18.1 | `Microsoft.ML.OnnxRuntime` 1.18.1 | 11.0 | 11.0, and has no `MLComputePlan` import |
| `libuiohook.dylib` | `SharpHook` 7.1.1 | 10.15 | |
| Avalonia, Skia, HarfBuzz | | 10.13 | |
| .NET 10 runtime | | 12.0 | Microsoft supports 15 and later only |

`minos` is a declaration, not a check: the dynamic loader does not refuse a library on an
older system, it fails only when a symbol is missing. That is why Whisper works on 13 and
ONNX Runtime does not.

Two consequences Parlotype is built around:

- SileroVad depends on `Microsoft.ML.OnnxRuntime.Gpu`, which has **no macOS natives at
  all**. On a Mac the managed ONNX Runtime finds the copy sherpa-onnx ships, as on
  Windows. Where that copy cannot load there is no voice activity detection, and so no
  dictation with any engine, Whisper and the cloud ones included.
- An older ONNX Runtime cannot stand in for sherpa-onnx's own: the C API library asks for
  1.24.4 by file name and by API version. It can stand in for the VAD, which is what
  `LegacyOnnxRuntime` does on Intel Macs below 14.4.

## How to check a new version

```bash
otool -l lib.dylib | grep -A4 LC_BUILD_VERSION | grep -E 'minos|sdk'
nm -m -u lib.dylib | grep MLComputePlan      # any hit means macOS 14.4+
```

## Publish filters that assumed Windows

All three are in `Directory.Build.targets`; each failed the first macOS publish.

- **Whisper.net names its folders `macos-x64` and `macos-arm64`**, the SDK's
  RuntimeIdentifier is `osx-x64` / `osx-arm64`. The ADR-051 filter keeps "the folder named
  like the RID", so it kept none and the app shipped without Whisper.
- **`Microsoft.ML.OnnxRuntime.Gpu.Windows` copies `onnxruntime.dll` into every output.**
  Its only platform check is on the processor. A macOS publish carried 11 MB of Windows
  code, which then also needed a code signature inside the bundle.
- **The same package breaks an arm64 build outright.** For an arm64 target its copy items
  name `runtimes/win-arm64/native/onnxruntime.dll`, which the package does not contain,
  and the build stops with MSB3030. The items have to be withdrawn before
  `AssignTargetPaths`, not filtered at publish.
