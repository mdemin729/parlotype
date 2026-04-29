---
title: Parlotype.Desktop
type: service-profile
status: active
tags: [desktop, avalonia, ui, mvvm]
criticality: high
last_updated: 2026-04-28
summary: Avalonia UI application — entry point, DI wiring, views and viewmodels
---

# Parlotype.Desktop

## Purpose
The main application. Avalonia 11.3.0 with Fluent theme. Wires DI container, hosts all views and viewmodels.

## Key Paths
- `src/Parlotype.Desktop/ViewModels/` — MVVM viewmodels using CommunityToolkit.Mvvm
- `src/Parlotype.Desktop/Views/` — AXAML views with compiled bindings
- `src/Parlotype.Desktop/App.axaml.cs` — DI container setup
- `src/Parlotype.Desktop/Program.cs` — Entry point

## Launch
```bash
dotnet run --project src/Parlotype.Desktop
```

## Conventions
- `x:CompileBindings="True"` and `x:DataType` on all AXAML
- Never use `{ReflectionBinding}`
- `[ObservableProperty]` on private fields, `[RelayCommand]` on methods
- ViewModels must be `partial` classes
- Flyout bindings: embed commands in display item wrappers, not `$parent` traversal
- Flyout lifecycle: code-behind hooks `PopupFlyoutBase.Opening` for refresh
- `ObservableCollection` mutations from background threads → dispatch to `Dispatcher.UIThread`
- `App.axaml.cs` runs a fire-and-forget `Task.Run` after `BuildServiceProvider` to call `INvidiaEnvironmentProvider.GetAsync` and log the result (driver, installed CUDA toolkits, loadable runtimes) at Information level — see [[decisions/_index|ADR-014]]

## Dependencies
- [[platform]], [[core]]
- Avalonia 11.3.0, CommunityToolkit.Mvvm, Microsoft.Extensions.DependencyInjection
