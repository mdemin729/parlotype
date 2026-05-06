---
title: "Session: 2026-05-06 — Vulkan runtime support"
type: session
status: active
tags: [vulkan, whisper, runtime, settings, adr-022]
created: 2026-05-06
summary: Added Vulkan as a third Whisper.net runtime alongside CUDA and CPU, with strict no-fallback semantics for explicit Cuda/Vulkan picks, a new Settings → Runtime section, startup detection, and ADR-022.
---

# Session: 2026-05-06

## Active Focus

End-to-end Vulkan integration:

- **Core (`Parlotype.Core/Speech/`)**: extended `RuntimePreference` to `Auto / Cuda / Vulkan / Cpu`; added `RuntimeUnavailableException`; added `IVulkanEnvironmentProvider`, `VulkanEnvironmentInfo`, `VulkanDeviceInfo`, `VulkanDeviceType`.
- **Platform (`Parlotype.Platform/Speech/`)**: `WhisperRuntimeBootstrap` switch covers all four prefs (Auto = `[Cuda, Vulkan, Cpu]`); `WhisperSpeechRecognizer` constructor now takes both env providers and pre-flight-checks the requested runtime before model download; `WindowsVulkanEnvironmentProvider` (P/Invoke into `vulkan-1.dll`); `NoOpVulkanEnvironmentProvider` for non-Windows.
- **Packaging**: `Whisper.net.Runtime.Vulkan` 1.9.0 added unconditionally to `Parlotype.Platform.csproj`.
- **Desktop**: new **Settings → Runtime** section — `RuntimeDisplayItem`, `RuntimeSettingsViewModel`, `RuntimeSettingsView.axaml(.cs)` mirroring the WhisperModel pattern. Wired into `SettingsWindow.axaml`, `SettingsWindowViewModel`, `App.axaml.cs` DI. `App.axaml.cs` now logs Vulkan environment alongside NVIDIA. `TranscribeViewModel` catches `RuntimeUnavailableException` and surfaces a status-bar message.
- **Tests**: updated `WhisperRuntimeBootstrapTests` + `WhisperRuntimeFallbackTests` for new Auto order and strict modes; added `WhisperSpeechRecognizerStrictRuntimeTests`, `WindowsVulkanEnvironmentProviderTests`, `RuntimeSettingsViewModelTests`; added `MockNvidiaEnvironmentProvider` + `MockVulkanEnvironmentProvider`; updated `SettingsWindowViewModelTests` for the 6-section list.
- **Docs**: README (Vulkan prerequisites + GPU paragraph rewrite), new ADR-022, memory vault (`decisions/_index`, `architecture/subsystems` — added Vulkan + Whisper Runtime Selection sections, `architecture/audio-pipeline`, `services/core`, `services/platform`), `CLAUDE.md` ("GPU / CUDA Builds" → "GPU Runtime Builds").

Final state: 257 tests pass; build clean (one pre-existing AVLN5001 warning in `ModelDownloadDialog.axaml`, unrelated to this change).

## Decisions Made

- **Enum shape**: `Auto / Cuda / Vulkan / Cpu` (not "no Cuda" or "drop Auto"). Auto chains Cuda → Vulkan → Cpu silently; Cuda and Vulkan are strict.
- **Packaging**: `Whisper.net.Runtime.Vulkan` is **unconditional** (no `EnableVulkan` MSBuild flag). Package is small (~30 MB) so no need to mirror the `EnableCuda` opt-out pattern.
- **Detection depth**: Full `IVulkanEnvironmentProvider` mirroring `INvidiaEnvironmentProvider` (interface in Core, Win32 impl, no-op fallback) — not a lightweight DLL-presence probe.
- **Strict semantics**: explicit `Cuda` / `Vulkan` throw `RuntimeUnavailableException` when their environment isn't detected. UI surfaces this in `TranscribeViewModel.StatusText` as "{Runtime} runtime not available — change in Settings". The user explicitly requested this over silent CPU fallback (transcript: *"Show error message if CUDA is not ready"*).
- **Pre-flight ordering**: runtime availability check runs **before** `EnsureModelAsync` in both `InitializeAsync` overloads — fail fast without downloading a model that won't be used.
- **UI restart-required model**: runtime selection only persists; `RuntimeOptions.RuntimeLibraryOrder` is process-global and one-shot (per ADR-012). The View shows "Changes take effect after restart."

## Facts Learned

- `RuntimeOptions.LoadedLibrary` (Whisper.net) is `null` until the first `WhisperFactory` is created. Useful as a post-load assertion to confirm strict-mode chose the right backend.
- Vulkan version packing (used by `vkEnumerateInstanceVersion`) is `variant<<29 | major<<22 | minor<<12 | patch`. `VK_MAKE_API_VERSION(0, 1, 3, 0) = 0x403000`, not `0x402000` — easy off-by-one to hit when writing test fixtures.
- `VkPhysicalDeviceProperties` C struct is 824 bytes total but the head is sequential and stable (`apiVersion` u32, `driverVersion` u32, `vendorID` u32, `deviceID` u32, `deviceType` i32, `deviceName[256]`). Allocating an 824-byte buffer and reading the first 276 bytes via `Marshal.ReadInt32` + `Marshal.Copy` is the simplest way to extract device info without modelling the entire struct in C#.
- xUnit v3 enforces `xUnit1051` (TestContext.Current.CancellationToken) under `TreatWarningsAsErrors=true`. Any `Task.Delay`, `ISettingsService.GetAsync/SetAsync` etc. inside a `[Fact]` must propagate the cancellation token.
- `git stash` does **not** include untracked files by default — running `git stash && dotnet build` while new files are untracked left them in the working tree referencing reverted symbols and produced misleading errors. Add `-u` (or `--include-untracked`) when you actually want a clean rollback.
- The pre-existing AVLN5001 warning in `ModelDownloadDialog.axaml` (`Window.SystemDecorations` obsolete) sometimes appears and sometimes doesn't depending on whether the file is in the incremental compile graph. It's not new in this branch — `git log -- src/Parlotype.Desktop/Views/ModelDownloadDialog.axaml` shows the last touch was months ago.

## Open Blockers

None. End-to-end manual verification on a real Vulkan-capable GPU host wasn't performed in this session — the integration test path through `WhisperFactory.FromPath` requires native Vulkan libs at runtime, which the dev box may or may not have. Worth running once on a target machine.

## Documentation Status

- ADR: **done** — `docs/decisions/022-vulkan-gpu-acceleration.md`
- Vault (services/architecture): **done** — updated `memory/decisions/_index.md`, `memory/architecture/subsystems.md` (added Vulkan + Whisper Runtime Selection sections), `memory/architecture/audio-pipeline.md`, `memory/services/core.md`, `memory/services/platform.md`; also `CLAUDE.md` and `README.md`.
- Knowledge (non-derivable facts): **done** — `memory/knowledge/vulkan-runtime-probing.md` (Vulkan version packing, `VkPhysicalDeviceProperties` head-only marshalling, `RuntimeOptions.LoadedLibrary` semantics). Indexed in `memory/knowledge/_index.md`.

## Next Action

**End-to-end verification on real hardware.** Launch `dotnet run --project src/Parlotype.Desktop` on a Vulkan-capable machine (e.g. AMD or Intel GPU host without CUDA), set `RuntimePreference` to `Vulkan` via **Settings → Runtime**, restart, record a clip, and confirm the log line `Whisper runtime loaded: Vulkan` in `%LOCALAPPDATA%/parlotype/logs/`. Then flip to `Cuda` on the same machine to verify the strict no-fallback error path surfaces "Cuda runtime not available — change in Settings" in the transcribe status bar (no silent CPU fallback).

If end-to-end works, the change is shippable. If a native-load surprise comes up (Vulkan loader present but no compatible ICD, etc.), the catch in `WhisperSpeechRecognizer.InitializeAsync` should already wrap it as `RuntimeUnavailableException` — verify the user-facing message is clear enough.
