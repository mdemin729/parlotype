---
title: Implement Feature Skill
type: skill
status: active
tags: [skill, feature, development, workflow]
last_updated: 2026-03-28
summary: Step-by-step guide for adding new features to Parlotype
---

# Implement Feature

## Before You Start

1. Read [[conventions/_index]] for coding standards
2. Read [[services/_index]] to identify affected projects
3. Check [[decisions/_index]] for relevant design constraints

## Implementation Workflow

### 1. Define the Contract (Core)
- Add interface(s) to `src/Parlotype.Core/` in the appropriate subfolder
- Define models, records, or enums needed
- Keep Core dependency-free — interfaces and models only

### 2. Implement (Platform)
- Add implementation class to `src/Parlotype.Platform/` mirroring Core's subfolder
- Register in `PlatformServiceExtensions.cs` as singleton
- Use existing platform dependencies (NAudio, Whisper.net, SharpHook, etc.)

### 3. Wire UI (Desktop)
- Add ViewModel to `src/Parlotype.Desktop/ViewModels/` (must be `partial`)
- Use `[ObservableProperty]` and `[RelayCommand]`
- Add View (`.axaml`) to `src/Parlotype.Desktop/Views/`
- Use `x:CompileBindings="True"` and `x:DataType`
- Extract reusable UI into UserControls

### 4. Write Tests
- Core/Platform tests in `src/Parlotype.Tests/`
- UI tests in `src/Parlotype.Desktop.Tests/` with `[AvaloniaFact]`
- Use mocks from `Mocks/` folder for controllable testing

### 5. Verify
```bash
dotnet build Parlotype.slnx          # Zero warnings
dotnet test                           # All tests pass
dotnet run --project src/Parlotype.Desktop  # Manual verification
```

### 6. Document
- If architectural decision: create ADR in `docs/decisions/` (next: 013)
- Update relevant service profiles in `memory/services/`
- Record session in `memory/sessions/`

## Checklist
- [ ] Interface in Core
- [ ] Implementation in Platform
- [ ] Registered in `PlatformServiceExtensions.cs`
- [ ] ViewModel + View in Desktop (if UI)
- [ ] Tests written and passing
- [ ] Zero build warnings
- [ ] ADR if needed
