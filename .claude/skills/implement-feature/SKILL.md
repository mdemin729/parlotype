---
name: implement-feature
description: Use when adding a new feature to Parlotype that spans Core/Platform/Desktop layers. Walks through the contract-first workflow (Core interface → Platform impl → DI registration → Desktop UI → tests → ADR/docs).
---

# Implement Feature

## Before You Start

1. Read `memory/conventions/_index.md` for coding standards
2. Read `memory/services/_index.md` to identify affected projects
3. Check `memory/decisions/_index.md` for relevant design constraints

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
Documentation is part of the workflow, not a follow-up. Decide each question explicitly:

- **Did you add an interface, record, or enum to `Parlotype.Core`?** → Yes ⇒ ADR + `memory/services/core.md` update
- **Did you add a registration to `PlatformServiceExtensions.cs`?** → Yes ⇒ ADR + `memory/services/platform.md` update
- **Did you add a `.csproj` dependency or native library?** → Yes ⇒ ADR
- **Does behaviour now differ by OS or build flag?** → Yes ⇒ ADR
- **Did you touch audio pipeline / hotkeys / settings / Whisper / text injection?** → Yes ⇒ ADR + relevant subsystems.md section
- **Did you add a UI surface?** → Yes ⇒ `memory/services/desktop.md` update
- **Did you learn something non-derivable from code (third-party quirk, env gotcha)?** → Yes ⇒ `memory/knowledge/<topic>.md` + index row

If any answer above is "yes" and you're choosing to defer the docs, **ask the user first** via `ask_user` — do not silently ship.

ADRs go in `docs/decisions/` (use `_template.md`, sequential numbering — check the directory for the highest existing number, do not trust stale "next: NNN" hints in indexes). After writing the ADR, add a row to `memory/decisions/_index.md`.

## Checklist
- [ ] Interface in Core
- [ ] Implementation in Platform
- [ ] Registered in `PlatformServiceExtensions.cs`
- [ ] ViewModel + View in Desktop (if UI)
- [ ] Tests written and passing
- [ ] Zero build warnings
- [ ] End-to-end behaviour verified (manual run / log line / integration test)
- [ ] ADR written if any trigger fires (see Step 6)
- [ ] `memory/services/*.md` updated for new symbols
- [ ] `memory/decisions/_index.md` updated if new ADR
- [ ] `memory/architecture/subsystems.md` updated for new subsystems
- [ ] `memory/knowledge/` entry added for non-derivable facts
- [ ] If deferring docs, asked the user first

