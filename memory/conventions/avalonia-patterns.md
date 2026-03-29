---
title: Avalonia UI Patterns
type: convention
status: active
tags: [avalonia, axaml, mvvm, ui]
last_updated: 2026-03-28
summary: AXAML compiled bindings, MVVM with CommunityToolkit, flyout patterns, threading
---

# Avalonia UI Patterns

## MVVM with CommunityToolkit.Mvvm
- Use `[ObservableProperty]` on **private fields** (source generator creates public property)
- Use `[RelayCommand]` on **methods** (source generator creates ICommand)
- ViewModels must be **`partial` classes** (required for source generators)

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
