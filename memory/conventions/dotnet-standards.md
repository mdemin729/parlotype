---
title: .NET Standards
type: convention
status: active
tags: [dotnet, conventions, build]
last_updated: 2026-05-21
summary: .NET 10 target, nullable refs, warnings-as-errors, DI registration patterns, Whisper hot-swap, GPU runtime preference
---

# .NET Standards

## Build Requirements
- **Target framework**: `net10.0` (set in `Directory.Build.props`)
- **Nullable reference types**: enabled (`<Nullable>enable</Nullable>`)
- **Implicit usings**: enabled
- **Warnings as errors**: `TreatWarningsAsErrors=true` in `Directory.Build.props` — applies to every project
- Solution format: modern `.slnx` (see `Parlotype.slnx`)

## Architecture Rules

| Rule | Rationale |
|------|-----------|
| Interfaces in Core, implementations in Platform | Keep Core free of platform dependencies |
| Never add platform-specific packages to Core | Core must remain portable and dependency-free |
| All services are singletons | Registered in `PlatformServiceExtensions.cs` |
| New services → register in `PlatformServiceExtensions.cs` | Single DI registration point |

## Key Patterns
- New domain contract → interface in `Parlotype.Core` (appropriate subfolder)
- New implementation → class in `Parlotype.Platform` + register in `PlatformServiceExtensions.cs`
- New UI feature → ViewModel in `ViewModels/` + View in `Views/`
- Extract reusable UI → separate UserControl

## Whisper Model Lifecycle
- **Never** load two Whisper models simultaneously
- Sequential load → `UnloadAsync()` → load **is** supported and powers in-app model switching ([[decisions/_index|ADR-017]])
- `WhisperModelType` enum (Core) maps to `GgmlType` (Platform) via `WhisperModelTypeExtensions`
- Model choice persisted via `SettingsKeys.SelectedWhisperModel`; reread on settings change

## Speech Engine Selection
- `SettingsKeys.SpeechEngine` (`Whisper` / `Gemma4`) selects the active recognizer
- `DelegatingSpeechRecognizer` (Platform) routes to `WhisperSpeechRecognizer` or `LlamaCppSpeechRecognizer` based on this setting ([[decisions/_index|ADR-025]])
- Settings UI hides engine-restricted rows for the inactive engine ([[decisions/_index|ADR-028]])

## GPU Runtime Preference
- `RuntimePreference` enum: `Auto` (default) → `Cuda` → `Vulkan` → `Cpu`
- `Auto` chains CUDA → Vulkan → CPU silently
- `Cuda` and `Vulkan` are **strict** — throw `RuntimeUnavailableException` rather than silently falling back to CPU ([[decisions/_index|ADR-022]])
- Persisted under `SettingsKeys.RuntimePreference`; selection is process-global one-shot ([[decisions/_index|ADR-012]], [[decisions/_index|ADR-022]]) — changes require restart

## GPU Runtimes (packaging)
- `Whisper.net.Runtime.Cuda` (~350 MB) — included when `EnableCuda` MSBuild property is `true` (default). NVIDIA only.
- `Whisper.net.Runtime.Vulkan` (~30 MB) — **always included**. Works on AMD/Intel/NVIDIA.
- Build without CUDA: `dotnet build Parlotype.slnx -p:EnableCuda=false` (Vulkan stays).

