---
title: .NET Standards
type: convention
status: active
tags: [dotnet, conventions, build]
last_updated: 2026-03-28
summary: .NET 10 target, nullable refs, warnings-as-errors, DI registration patterns
---

# .NET Standards

## Build Requirements
- **Target framework**: `net10.0`
- **Nullable reference types**: enabled
- **Implicit usings**: enabled
- **Warnings as errors**: `TreatWarningsAsErrors=true` in `Directory.Build.props`
- Solution format: modern `.slnx`

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
- **Never** load the Whisper model multiple times in a single run
- `WhisperModelType` enum (Core) maps to `GgmlType` (Platform)
- Model choice persisted via `SettingsKeys.SelectedWhisperModel`

## GPU / CUDA
- `Whisper.net.Runtime.Cuda` included by default (~350 MB)
- Build without: `dotnet build -p:EnableCuda=false`
- Auto-detected at runtime, CPU fallback is silent
