---
status: accepted
date: 2026-07-31
---

# 048. Whisper Runtime Latch Detection & Factory Lifetime

## Context

A user switched **Settings → Runtime** from CUDA to Vulkan mid-session and pressed record. Every attempt failed with

```
RuntimeUnavailableException: Whisper runtime 'Vulkan' is not available:
Whisper.net loaded 'Cuda' instead of 'Vulkan'. The native runtime may be missing or incompatible.
```

and the process grew by ~3 GB per attempt (18 GB total with `large-v3`), while the managed heap stayed at ~22 MB.

Three defects behind one symptom:

1. **The message blamed the machine, not the constraint.** Vulkan was fine. Whisper.net resolves `RuntimeOptions.RuntimeLibraryOrder` **once per process** (ADR-012, ADR-022) and `WhisperRuntimeBootstrap.Initialize` is deliberately first-call-wins, so a preference changed after the first model load can only take effect after a restart. The Settings page carried a passive "Changes take effect after restart" line, but nothing detected or reported the pending state — the user found out at record time, from an error implying broken drivers.

2. **The mismatch was detected after the model was loaded.** `AssertLoadedRuntimeMatches` ran after `WhisperFactory.FromPath`, i.e. after multiple GB of weights were read into RAM/VRAM (the user's log shows `whisper_model_load: CUDA0 total size = 3094.36 MB` two seconds *before* the exception).

3. **That late throw leaked the model.** `_factory` was assigned before the assertion, then the assertion threw: `IsReady` stayed `false`, `_currentOptions` was not updated, and `UnloadAsync()` early-returned on `!IsReady` — so the factory was never disposed, and the next attempt simply overwrote the field. `WhisperFactory` holds its native context in a plain `IntPtr` (`Lazy<IntPtr>`) and declares **no finalizer**, so a dropped factory leaks for the lifetime of the process. Measured on `base.en`: 149 MB leaked per failed attempt, 746 MB over five.

Separately, the strict check only covered `Cuda` and `Vulkan`. Choosing **CPU** while a GPU runtime was latched passed silently and kept running on the GPU — the user's own log has `Runtime=Cpu` followed by `Whisper runtime loaded: Cuda` with no warning.

## Decision

1. **Expose the latch as a Core contract.** New `IWhisperRuntimeStatus` (`Parlotype.Core/Speech/`) with `LoadedRuntimeName` and `RequiresRestartFor(RuntimePreference)`; implemented by `WhisperRuntimeStatus` in Platform over `WhisperRuntimeBootstrap`, registered as a singleton. Desktop cannot see Whisper.net types, and this keeps it that way.
2. **Matching lives in one place.** `WhisperRuntimeBootstrap.IsSatisfiedBy(preference, loaded)`: `Auto` accepts whatever won the fallback chain, `Cuda`/`Vulkan` must match exactly, and `Cpu` accepts both `Cpu` and `CpuNoAvx` (Whisper.net chooses the AVX variant itself). Nothing loaded ⇒ satisfied.
3. **Fail before the load, not after.** `WhisperSpeechRecognizer.AssertRuntimeStillSelectable` runs before the model download in both `InitializeAsync` overloads. The post-load assertion stays for the case where the *order* was latched by an earlier failed init.
4. **`RuntimeUnavailableException.RequiresRestart`** distinguishes "restart to apply" from "this machine can't do it". Both new guards set it; `TranscribeViewModel` shows *"Restart Parlotype to use the X runtime"* instead of sending the user to Settings they already changed.
5. **The factory is published only when fully built.** `CreateVerifiedFactory` disposes the factory if verification fails; the processor build is wrapped so a failure there disposes too; `_factory` is assigned last. `UnloadAsync` no longer gates on `IsReady` — it releases whatever exists.
6. **Strict CPU.** The preference check now covers `Cpu`, so a CPU selection under a latched GPU runtime reports the pending restart instead of silently running on the GPU.
7. **Settings shows the pending state.** `RuntimeSettingsViewModel` exposes `RestartRequired` / `LoadedRuntimeName`, and `RuntimeSettingsView` renders a "Restart required" panel naming both the loaded and the selected runtime.

## Consequences

- **Easier:** The failure is now instant, self-explanatory, and free — no multi-GB load before the diagnosis, and the user is told the actual remedy in Settings, before they try to record.
- **Easier:** No failed initialization can strand a native model context. Verified end-to-end: the same five-attempt sequence that leaked 746 MB (`base.en`) now grows 0 MB.
- **Easier:** `IWhisperRuntimeStatus` is injectable, so tests exercise latch behaviour without mutating Whisper.net's process-global statics.
- **Harder:** Selecting **CPU** while a GPU runtime is latched is now an error until restart, where it used to (silently, wrongly) keep working. This is the honest behaviour and matches the other strict modes.
- **Harder:** `WhisperSpeechRecognizerTests` and `AudioPipelineTests` join the `WhisperRuntime` xUnit collection — they load real models, so they must not interleave with classes that mutate the runtime order. `WhisperRuntimeFallbackTests` now asserts that `Initialize` does not *change* the loaded library rather than that none is loaded, since the latch is per-process for the whole run.
- **Unchanged:** Runtime selection remains process-global and one-shot. This ADR makes the constraint visible and cheap to hit, it does not remove it — doing so would require unloading the native library, which Whisper.net does not support.
