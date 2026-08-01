---
title: "Session: 2026-07-31 — Whisper runtime latch & factory leak"
type: session
status: active
tags: [whisper, runtime, vulkan, memory-leak, adr-048]
created: 2026-07-31
summary: "Diagnosed the 'loaded Cuda instead of Vulkan' error and the ~3 GB-per-attempt memory growth behind it; fixed both under ADR-048"
---

# Session: 2026-07-31

## Active Focus
Worktree `whisper-vulkan-runtime-error-22498d`. User reported: switching Settings → Runtime from CUDA to Vulkan mid-session made every record press fail with `RuntimeUnavailableException` while the process grew ~3 GB per attempt (18 GB total, managed heap flat at ~22 MB).

Touched: `WhisperSpeechRecognizer`, `WhisperRuntimeBootstrap`, new `WhisperRuntimeStatus`, new Core `IWhisperRuntimeStatus`, `RuntimeUnavailableException`, `PlatformServiceExtensions`, `RuntimeSettingsViewModel`/`View`, `TranscribeViewModel`, `WhisperRuntimeLatchTests` (new), `WhisperRuntimeFallbackTests`, collection attributes on `WhisperSpeechRecognizerTests`/`AudioPipelineTests`.

## Decisions Made
- [[decisions/_index|ADR-048]] accepted: detect the process-wide runtime latch *before* the model download and never let a `WhisperFactory` escape a failed init.
- Strict `Cpu` is now enforced like `Cuda`/`Vulkan` — a CPU selection under a latched GPU runtime errors instead of silently running on the GPU (the user's log showed exactly that happening).
- The latch is injected (`IWhisperRuntimeStatus`) rather than read statically, so tests never mutate `RuntimeOptions.LoadedLibrary`.
- `RuntimeUnavailableException.RequiresRestart` drives distinct UX: "Restart Parlotype to use the X runtime" vs "change in Settings".

## Facts Learned
- `WhisperFactory` has **no finalizer** and holds its native context in a bare `Lazy<IntPtr>`; `FromPath` loads full weights eagerly. An undisposed factory leaks the whole model for the process lifetime — 149 MB per attempt with `base.en`, ~3 GB with `large-v3`. Captured in [[whisper-net-quirks]] §3.
- The old `UnloadAsync` gate (`if (!IsReady) return;`) was what made the leak permanent: `IsReady` is set only after a *successful* init, so the half-initialized factory was unreachable to cleanup and got overwritten on the next attempt.
- `RuntimeOptions.LoadedLibrary` has a public static setter, but mutating it in tests breaks the classes that load real models in parallel — hence the DI seam and the shared `WhisperRuntime` xUnit collection.
- `RuntimeLibrary` in Whisper.net 1.9.0: `Cpu, Cuda, Vulkan, CoreML, OpenVino, CpuNoAvx` — `Cpu` matching must accept `CpuNoAvx`.

## Open Blockers
- None. Verified: full suite green (1028 tests), CUDA and `EnableCuda=false` builds clean, leak reproduced (746 MB / 5 attempts) and shown gone (0 MB) with a temporary probe that was then deleted.
- Not exercised by an agent: the actual Settings → Runtime "Restart required" panel rendering (would need launching the app, which would fight the user's running instance for global hotkeys).

## Documentation Status
- ADR: done — `docs/decisions/048-whisper-runtime-latch-and-factory-lifetime.md`
- Vault (services/architecture): done — [[services/core]], [[services/platform]], [[services/desktop]], [[architecture/subsystems]], [[decisions/_index]]
- Knowledge: done — [[whisper-net-quirks]] §3 + index row

## Next Action
Changes are uncommitted on `claude/whisper-vulkan-runtime-error-22498d`. Next: have the user restart the app and confirm the "Restart required" panel + instant record-time message, then commit/PR.
