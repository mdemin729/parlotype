---
title: "Whisper.net Quirks"
area: "speech-engines"
summary: "Whisper.net 1.9.0 inverts ggml log levels, WhisperFactory has no finalizer so a dropped one leaks the model, and model load blocks the calling thread."
verified: 2026-11-11
---

# Whisper.net 1.9.0 Quirks

Non-derivable facts about the Whisper.net 1.9.0 NuGet packages. They are stable across
1.9.x patch releases and likely to stay relevant until Whisper.net 2.x.

## 1. The NuGet build can differ from upstream `master`

The Whisper.net NuGet build can differ from upstream `master` (1.9.0's `CudaHelper`
hard-coded `cudart64_13` while `master` probed several versions); read the tag matching the
consumed package (e.g. `v1.9.0`), or decompile it with `ilspycmd`. ADR-014's NVIDIA detection
([ADR-014](../decisions/014-nvidia-environment-detection.md)) deliberately does not depend on
Whisper.net internals.

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

The native value is passed straight through, so a native `INFO=2` arrives as managed
`WhisperLogLevel.Error`. Naively bridging Whisper.net logs to an application logger tags
every benign INFO message as Error.

**Workaround**: `WhisperRuntimeBootstrap` remaps:
- `WhisperLogLevel.Error` → `LogLevel.Information`
- `WhisperLogLevel.Warning` → `LogLevel.Debug`
- `WhisperLogLevel.Info` → `LogLevel.Trace`

This is intentionally pessimistic; revisit if Whisper.net fixes the enum in a future version.

**Verification**: `Whisper.net.Internals.Native.Data.GgmlLogLevel` in the decompiled NuGet
assembly.

## 3. `WhisperFactory` has **no finalizer**: dropping one leaks the whole model

`WhisperFactory` keeps its native context in a plain `Lazy<IntPtr>` field (`contextLazy`) and
declares no `Finalize` override; `WhisperProcessor` is the same with a raw `IntPtr
currentWhisperContext`. Nothing releases `whisper_free` except an explicit
`Dispose`/`DisposeAsync`.

Consequences hit in [ADR-048](../decisions/048-whisper-runtime-latch-and-factory-lifetime.md):

- `WhisperFactory.FromPath` loads the **full model weights immediately** (the native log
  prints `whisper_model_load: … total size = N MB` before the call returns), so a factory that
  is created and then dropped on an exception path leaks that many MB for the process lifetime.
- The managed object is a few bytes, so the GC has no pressure signal and never collects on
  its account: the managed heap stays flat (~22 MB) while private bytes climb. Measured:
  149 MB leaked per failed initialization with `base.en`, ~3 GB with `large-v3`.
- Any code path that creates a factory must dispose it on **every** failure branch. Guarding
  disposal on an `IsReady`-style flag that is only set at the end of a successful init is
  exactly the bug.

**Verification**: reflect over `Whisper.net.WhisperFactory`:
`GetMethod("Finalize", NonPublic|Instance).DeclaringType` is `System.Object`, i.e. no custom
finalizer.

## 4. Native libraries are plain content, not RID-scoped assets

Natives ship as plain `<None>` content with hard-coded `TargetPath`s, so `publish -r <rid>`
cannot filter them; see `Directory.Build.targets` and
[ADR-051](../decisions/051-publish-only-target-rid-runtimes.md).

## 5. Model load is synchronous despite the async API

`WhisperFactory.FromPath` and `builder.Build()` are CPU-bound; wrap them in `Task.Run` or they
freeze the UI thread (and any `DispatcherTimer`-driven loading animation)
([ADR-038](../decisions/038-speech-model-prewarm-and-loading-state.md)).

## 6. Prefer post-processing over `WithSuppressTokens()` for profanity

Whisper.net 1.9.0 has `WithSuppressTokens()` for token-level filtering, but a post-processing
regex is more reliable for profanity, because token IDs vary by model.
