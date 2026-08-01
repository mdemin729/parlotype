---
title: Whisper.net Quirks
type: knowledge
tags: [whisper, cuda, logging, gotchas]
created: 2026-04-28
last_updated: 2026-07-31
summary: Non-obvious behaviours of Whisper.net 1.9.0 — CUDA diagnostics, inverted log levels, and unfinalized native model contexts
---

# Whisper.net 1.9.0 Quirks

Two non-derivable facts learned while diagnosing why our CUDA runtime fell back to CPU. Both are stable across patch releases of 1.9.x and likely to remain relevant until Whisper.net 2.x.

## 1. NuGet build of `CudaHelper` differs from upstream `master`

The Whisper.net source at `github.com/sandrohanea/whisper.net` `master` has multi-runtime support: probes `cudart64_13`, `cudart64_12`, etc., and reports the runtime version it loaded.

The published **NuGet package 1.9.0** is older. Its `CudaHelper` is hard-coded to a single DLL name (`cudart64_13`) and exposes no runtime version. If you check out the repo to understand behaviour, check the tag matching the NuGet you actually consume (e.g. `v1.9.0`), not `master`.

**Implication**: any introspection we add today against Whisper.net internals risks breaking on upgrade. Our [[decisions/_index|ADR-014]] provider deliberately re-implements detection rather than depending on Whisper.net.

**Verification**: decompile via `ilspycmd "<package>/lib/net8.0/Whisper.net.dll"` and inspect `LibraryLoader/CudaHelper`.

## 2. `WhisperLogLevel` enum is inverted vs native ggml

Native `ggml`/`whisper.cpp` emits log records using:

```
GGML_LOG_LEVEL_ERROR = 0
GGML_LOG_LEVEL_WARN  = 1
GGML_LOG_LEVEL_INFO  = 2
```

The managed `Whisper.net.WhisperLogLevel` enum in 1.9.0 has the **inverse ordering**:

```
WhisperLogLevel.Error   = 2
WhisperLogLevel.Warning = 3
WhisperLogLevel.Info    = 4
```

The native value is passed straight through, so a native `INFO=2` arrives as managed `WhisperLogLevel.Error`. Naively bridging Whisper.net logs to your application logger will tag every benign INFO message as Error.

**Workaround**: `WhisperRuntimeBootstrap` remaps:
- `WhisperLogLevel.Error` → `LogLevel.Information`
- `WhisperLogLevel.Warning` → `LogLevel.Debug`
- `WhisperLogLevel.Info` → `LogLevel.Trace`

This is intentionally pessimistic; revisit if Whisper.net fixes the enum in a future version.

**Verification**: `Whisper.net.Internals.Native.Data.GgmlLogLevel` in the decompiled NuGet assembly.

## 3. `WhisperFactory` has **no finalizer** — dropping one leaks the whole model

`WhisperFactory` keeps its native context in a plain `Lazy<IntPtr>` field (`contextLazy`) and declares no `Finalize` override; `WhisperProcessor` is the same with a raw `IntPtr currentWhisperContext`. Nothing releases `whisper_free` except an explicit `Dispose`/`DisposeAsync`.

Consequences we hit in [[decisions/_index|ADR-048]]:

- `WhisperFactory.FromPath` loads the **full model weights immediately** (the native log prints `whisper_model_load: … total size = N MB` before the call returns), so a factory that is created and then dropped on an exception path leaks that many MB for the process lifetime.
- The managed object is a few bytes, so the GC has no pressure signal and never collects on its account — the managed heap stays flat (~22 MB) while private bytes climb. Measured: 149 MB leaked per failed initialization with `base.en`, ~3 GB with `large-v3`.
- Any code path that creates a factory must dispose it on **every** failure branch. Guarding disposal on an `IsReady`-style flag that is only set at the end of a successful init is exactly the bug.

**Verification**: reflect over `Whisper.net.WhisperFactory` — `GetMethod("Finalize", NonPublic|Instance).DeclaringType` is `System.Object`, i.e. no custom finalizer.
