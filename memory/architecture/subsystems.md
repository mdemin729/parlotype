---
title: Key Subsystems
type: architecture
status: active
tags: [architecture, subsystems, hotkeys, settings, logging]
last_updated: 2026-04-28
summary: Text injection, global hotkeys, settings, logging, and model management subsystems
---

# Key Subsystems

## Text Injection

Two implementations of `ITextInjectionService`:

| Implementation | Mechanism | Default? |
|---------------|-----------|----------|
| `ClipboardTextInjectionService` | Save clipboard → set text → Ctrl+V → restore | Yes |
| `SharpHookTextInjectionService` | Direct key simulation via SharpHook | No |

`Win32TargetWindowTracker` tracks the last non-Parlotype foreground window to know where to inject text.

## Global Hotkeys

- **Core**: `IGlobalHotkeyService`, `HotkeyBinding` record (modifiers + key name string)
- **Platform**: `SharpHookHotkeyService` using `TaskPoolGlobalHook` for non-blocking dispatch
- **Mapping**: `KeyCodeMapper` converts Core key names → SharpHook `KeyCode`
- **Modes**: Push-to-Talk (key-down → start, key-up → stop) and Toggle
- **Suppression**: `SuppressEvent` prevents hotkey passthrough (Windows/macOS only)
- **Conflict detection**: `HotkeyConflictDetector` warns on reserved OS shortcuts
- **UI**: `HotkeyRecorderView` captures key combos in settings flyout
- **Persistence**: `JsonSettingsService` stores `HotkeyModifiers`, `HotkeyKey`, `ActivationMode`

## Settings

- `ISettingsService` (Core) → `JsonSettingsService` (Platform)
- Persists to `%LOCALAPPDATA%/parlotype/settings.json`
- Thread-safe via `SemaphoreSlim`

## Logging

- ZLogger to console + rolling file
- Log directory: `%LOCALAPPDATA%/parlotype/logs/`

## Model Management

Pipeline: `IModelDownloadService` (Core) → `HttpModelDownloadService` (Platform) → `ModelDownloadDialogService` (Desktop)

- `WhisperModelType` enum (Core) maps to `GgmlType` (Platform) via `WhisperModelTypeExtensions`
- `WhisperModelInfo` holds static metadata (display name, disk size, SHA hash)
- Model choice persisted via `SettingsKeys.SelectedWhisperModel`
- Tests use `HeadlessModelDownloadService` (downloads without UI)

## NVIDIA/CUDA Environment Detection

First-party detection independent of Whisper.net — see [[decisions/_index|ADR-014]]. Provides startup diagnostics for why CUDA was/wasn't selected and a data source for a future diagnostics UI.

- **Core**: `INvidiaEnvironmentProvider`, `NvidiaEnvironmentInfo`, `CudaRuntimeProbe` in `Parlotype.Core/Speech/`
- **Platform (Windows)**: `WindowsNvidiaEnvironmentProvider` combines three failure-isolated sources:
  1. `nvidia-smi` parsing → driver version + driver max CUDA version
  2. Filesystem scan of `%ProgramFiles%\NVIDIA GPU Computing Toolkit\CUDA\v*` → installed toolkits
  3. `cudart` P/Invoke probe via `NativeLibrary.TryLoad` + `cudaRuntimeGetVersion` / `cudaDriverGetVersion` → loadable runtimes with versions
- **Platform (other OS)**: `NoOpNvidiaEnvironmentProvider` returns `NvidiaEnvironmentInfo.Empty`
- **DI**: selection in `PlatformServiceExtensions` via `OperatingSystem.IsWindows()`
- **Caching**: first call detects, result cached with `SemaphoreSlim`; `RefreshAsync` clears cache and re-runs
- **Startup hook**: `App.axaml.cs` fires `Task.Run` after `BuildServiceProvider`, logs Information line summarising driver/toolkits/runtimes
