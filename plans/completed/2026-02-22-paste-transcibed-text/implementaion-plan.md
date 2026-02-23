# Text Injection Implementation Plan

## Overview

Add a text injection feature that pastes transcribed text into the currently focused application. Two implementations share the same `ITextInjectionService` interface:

1. **Clipboard-with-restore** (default) — saves clipboard, sets transcribed text, simulates Ctrl+V, restores clipboard
2. **SharpHook `SimulateTextEntry`** — uses SharpHook's `EventSimulator.SimulateTextEntry(string)` to type text character-by-character

The user selects the mode via a command-line argument: `--text-injection-mode=sharp-hook`. No UI toggle.

## Architecture

```
Parlotype.Core/TextInjection/
  ITextInjectionService.cs          — interface: Task InjectTextAsync(string text)
  ITargetWindowTracker.cs           — interface: tracks previously focused window
  TextInjectionMode.cs              — enum: Clipboard, SharpHook

Parlotype.Platform/TextInjection/
  ClipboardTextInjectionService.cs  — clipboard-with-restore (Win32 P/Invoke)
  SharpHookTextInjectionService.cs  — SharpHook EventSimulator
  Win32TargetWindowTracker.cs       — SetWinEventHook-based foreground tracking

Parlotype.Desktop/
  Program.cs                        — parse --text-injection-mode arg
  App.axaml.cs                      — register chosen ITextInjectionService impl
  ViewModels/MainWindowViewModel.cs — inject ITextInjectionService, call from OnTranscriptionAvailable
```

## Focus management

The user may trigger recording from Parlotype's UI, so the target app loses focus. To inject text into the correct window:

- `ITargetWindowTracker` passively tracks foreground window changes using `SetWinEventHook(EVENT_SYSTEM_FOREGROUND)`
- It remembers the last foreground window that belongs to a different process than Parlotype
- Both `ITextInjectionService` implementations accept `ITargetWindowTracker` and call `ActivateTargetWindow()` before injecting text
- This also works for the future global hotkey flow (the window active when the hotkey is pressed becomes the target)

## Dependency direction

- Core defines `ITextInjectionService` + `TextInjectionMode` (no new packages)
- Platform implements both services (SharpHook already referenced; clipboard via Win32 P/Invoke)
- Desktop wires the chosen implementation into DI based on CLI arg

## Detailed steps

### 1. Core — define the contracts

- Create `src/Parlotype.Core/TextInjection/ITextInjectionService.cs`
  ```csharp
  public interface ITextInjectionService
  {
      Task InjectTextAsync(string text, CancellationToken cancellationToken = default);
  }
  ```
- Create `src/Parlotype.Core/TextInjection/ITargetWindowTracker.cs`
  ```csharp
  public interface ITargetWindowTracker : IDisposable
  {
      nint? TargetWindow { get; }
      bool ActivateTargetWindow();
  }
  ```
- Create `src/Parlotype.Core/TextInjection/TextInjectionMode.cs`
  ```csharp
  public enum TextInjectionMode { Clipboard, SharpHook }
  ```

### 2. Platform — SharpHook implementation

- Create `src/Parlotype.Platform/TextInjection/SharpHookTextInjectionService.cs`
- Use `SharpHook.EventSimulator.SimulateTextEntry(text)` 
- Activate target window via `ITargetWindowTracker` before simulating
- Log result via `ILogger`
- Throw on `UioHookResult` failure

### 3. Platform — Clipboard-with-restore implementation

- Create `src/Parlotype.Platform/TextInjection/ClipboardTextInjectionService.cs`
- Use Win32 P/Invoke for clipboard (OpenClipboard, GetClipboardData, SetClipboardData, CloseClipboard) — avoids WinForms/WPF dependency
- Activate target window via `ITargetWindowTracker` before pasting
- Use SharpHook's `EventSimulator` to simulate Ctrl+V keystroke (already a dependency)
- Flow:
  1. Save current clipboard contents (text format)
  2. Set clipboard to transcribed text
  3. Activate target window
  4. Simulate Ctrl+V via EventSimulator key sequence
  5. Wait ~150ms for target app to process paste
  6. Restore original clipboard contents

### 3b. Platform — Win32TargetWindowTracker

- Create `src/Parlotype.Platform/TextInjection/Win32TargetWindowTracker.cs`
- Use `SetWinEventHook(EVENT_SYSTEM_FOREGROUND, ...)` to track foreground window changes
- Remember the last foreground window from a different process (i.e., not Parlotype)
- `ActivateTargetWindow()` calls `SetForegroundWindow(handle)` to restore focus

### 4. Desktop — CLI argument parsing

- In `Program.cs`, parse `--text-injection-mode=sharp-hook` from `args`
- Store result in a static field or pass to `App` via a mechanism Avalonia supports
- In `App.axaml.cs`, register the appropriate `ITextInjectionService` implementation

### 5. Desktop — Wire into ViewModel

- Add `ITextInjectionService` parameter to `MainWindowViewModel` constructor
- In `OnTranscriptionAvailable`, call `_textInjectionService.InjectTextAsync(e.Result.Text)`
- Handle errors with logging (don't crash on injection failure)

### 6. Platform — Register in DI

- Update `PlatformServiceExtensions.cs` to expose a method or accept the mode
- Or let Desktop handle registration directly (simpler, since it owns the CLI args)

### 7. Tests

- Unit test `SharpHookTextInjectionService` (mock EventSimulator if possible)
- Unit test `ClipboardTextInjectionService` clipboard save/restore logic
- Verify DI wiring with both modes
