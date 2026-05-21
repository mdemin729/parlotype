---
title: Avalonia UI Patterns
type: convention
status: active
tags: [avalonia, avalonia12, axaml, mvvm, ui]
last_updated: 2026-05-21
summary: Avalonia 12 patterns — AXAML compiled bindings, MVVM with CommunityToolkit, flyout patterns, threading, FocusChangedEventArgs
---

# Avalonia UI Patterns

> Parlotype is on **Avalonia 12.0.2**. V1 (Avalonia 11) was retired in [[decisions/_index|ADR-018]]. Devtools use `AvaloniaUI.DiagnosticsSupport` + the `avdt` global tool ([[decisions/_index|ADR-016]]).

## MVVM with CommunityToolkit.Mvvm
- Use `[ObservableProperty]` on **private fields** (source generator creates public property)
- Use `[RelayCommand]` on **methods** (source generator creates ICommand)
- ViewModels must be **`partial` classes** (required for source generators)
- Avoid `AsyncRelayCommand` when the command is shared across items in an `ItemsControl` — its `CanExecute = false` while executing flickers every button. Prefer sync `RelayCommand` + fire-and-forget. See [[asyncrelaycommand-flicker]].

## AXAML Rules

| Rule | Example |
|------|---------|
| Always use compiled bindings | `x:CompileBindings="True"` |
| Always specify DataType | `x:DataType="vm:MainViewModel"` |
| Never use ReflectionBinding | Use `{Binding}` or `{CompiledBinding}` |
| File extension is `.axaml` | Not `.xaml` |

## Design-Time Data
- Use `<Design.DataContext>` with parameterless ViewModel constructors
- Back parameterless constructors with design stubs for sample data

## Flyout Patterns

### Binding
Flyouts are **disconnected from the visual tree**. Embed commands directly in display item wrappers:
- `MicrophoneDisplayItem`, `WaitTimeDisplayItem`, `WhisperModelDisplayItem`
- Do NOT use `$parent` traversal bindings

### Lifecycle
Avalonia flyouts lack MVVM-friendly lifecycle bindings:
- Use **code-behind** to hook `PopupFlyoutBase.Opening`
- Refresh ViewModel data when flyouts open
- See `SettingsFlyoutView.axaml.cs` for reference

## Conditional CSS Classes
Use `Classes.xxx="{Binding Property}"` with `<Window.Styles>` for visual state:
```xml
<Button Classes.recording="{Binding IsRecording}" />
```

## Threading
- `ObservableCollection` mutations from background threads must dispatch to:
  ```csharp
  Avalonia.Threading.Dispatcher.UIThread
  ```

## Avalonia 12 Migration Gotchas
- `OnLostFocus(RoutedEventArgs)` → **`OnLostFocus(FocusChangedEventArgs)`** (from `Avalonia.Input`). Override signatures must be updated.
- Classic `Avalonia.Diagnostics` (F12 inspector) is retired in Avalonia 12 — use `AvaloniaUI.DiagnosticsSupport` + `avdt` instead ([[avalonia-devtools]], [[decisions/_index|ADR-016]]).
- Avalonia 12 build emits `BuildServices` telemetry to avaloniaui.net; the free community tier cannot opt out (see [[avalonia-devtools]]).

