---
status: accepted
date: 2026-04-30
---

# 017. Whisper Model Hot-Swap via UnloadAsync

## Context

When a user selects a different Whisper model in settings, the running model and active recording should respond immediately. Previously, model selection only persisted the setting — the old model continued to be used until the next app restart or recording session. `ISpeechRecognizer` had no way to release resources without permanent disposal (`DisposeAsync` sets `_disposed = true`, making the singleton unusable).

## Decision

Add a `Task UnloadAsync()` method to the `ISpeechRecognizer` Core interface. Unlike `DisposeAsync`, `UnloadAsync` releases the `WhisperProcessor` and `WhisperFactory` and resets `IsReady = false` without setting `_disposed`, allowing the recognizer singleton to be re-initialized with the newly selected model on the next recording.

The `WhisperModelSettingsViewModel` now coordinates model changes:
1. **UI update first** — `Apply(type)` runs synchronously so the selection indicator moves instantly (no button-disable flicker).
2. **Async cleanup** — fire-and-forget `ApplyModelChangeAsync`: stops recording if active, calls `UnloadAsync` if model is loaded, persists the setting.
3. **Next recording** — `AudioPipelineService.StartAsync` sees `!_recognizer.IsReady` and calls `InitializeAsync`, which reads the new setting and loads the new model.

The command stays synchronous (`RelayCommand`, not `AsyncRelayCommand`) to avoid `CanExecute` toggling that causes visible list flicker in Avalonia's `ItemsControl`.

## Consequences

- **Easier**: Users can switch models without restarting the app or manually stopping recording.
- **Easier**: `DisposeAsync` now delegates to `UnloadAsync`, eliminating duplicated cleanup code.
- **Constraint relaxed**: The "never load the Whisper model multiple times in a single run" rule is relaxed to "never load multiple models simultaneously" — sequential load→unload→load is now supported.
- **Interface expanded**: All `ISpeechRecognizer` implementors must consider `UnloadAsync`. The default interface method (`=> Task.CompletedTask`) keeps existing implementations safe.
