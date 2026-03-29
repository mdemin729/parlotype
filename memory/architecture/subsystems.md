---
title: Key Subsystems
type: architecture
status: active
tags: [architecture, subsystems, hotkeys, settings, logging]
last_updated: 2026-03-28
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
