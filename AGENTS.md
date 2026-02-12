# AGENTS.md — Parlotype

Instructions for AI agents working on this codebase.

## Architecture

Parlotype is a local-first voice-to-text desktop app with four projects:

- **Parlotype.Core** — Domain interfaces and models. Zero external dependencies. All contracts live here.
- **Parlotype.Platform** — Implements Core interfaces with real libraries (Whisper.net, NAudio, SharpHook).
- **Parlotype.Desktop** — Avalonia UI app. Entry point. Wires DI, hosts views/viewmodels.
- **Parlotype.Tests** — xUnit tests referencing Core and Platform.

**Dependency direction:** Desktop → Platform → Core. Tests → Core, Platform.

## Coding Conventions

- **Target framework:** .NET 10 (`net10.0`)
- **Nullable reference types:** Enabled globally — never suppress without justification
- **Warnings as errors:** Enabled — all warnings must be resolved
- **Implicit usings:** Enabled
- **MVVM pattern:** Use `CommunityToolkit.Mvvm` with source generators (`[ObservableProperty]`, `[RelayCommand]`)
- **DI:** `Microsoft.Extensions.DependencyInjection` — register services in `PlatformServiceExtensions.cs`
- **Interfaces in Core, implementations in Platform** — never add platform-specific packages to Core

## Build & Test

```bash
dotnet build Parlotype.sln      # Must compile with zero warnings
dotnet test                      # All tests must pass
dotnet run --project src/Parlotype.Desktop  # Launch the app
```

## Key Patterns

- New domain contracts → add interface to `Parlotype.Core` in the appropriate subfolder
- New platform implementations → add to `Parlotype.Platform` and register in `PlatformServiceExtensions.cs`
- New UI features → add ViewModels and Views to `Parlotype.Desktop`
- Always write tests for logic in Core and Platform
