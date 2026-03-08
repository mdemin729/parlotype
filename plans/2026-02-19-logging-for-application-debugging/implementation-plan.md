---
title: ZLogger logging
status: completed
created: 2026-02-19
started: 2026-02-19
completed: 2026-02-19
---

# Implement ZLogger Logging for Application Debugging

## Problem
No logging exists in the application. Need structured logging for debugging audio pipeline, transcription, device changes, and settings — using ZLogger v2 per research recommendation.

## Approach
Add `Microsoft.Extensions.Logging.Abstractions` to Core (zero-dep logging interface), `ZLogger` to Platform and Desktop. Wire up console (colored, dev) + rolling file logging. Inject `ILogger<T>` into all Platform services and key ViewModels.

## Workplan

### Phase 1: Add packages and wire logging infrastructure
- [ ] Add `Microsoft.Extensions.Logging.Abstractions` to Parlotype.Core.csproj
- [ ] Add `ZLogger` to Parlotype.Platform.csproj
- [ ] Add `ZLogger` + `Microsoft.Extensions.Logging` to Parlotype.Desktop.csproj
- [ ] Configure ZLogger (console + rolling file) in `App.axaml.cs` via `services.AddLogging(...)`
- [ ] Build, commit

### Phase 2: Add logging to Platform services
- [ ] `AudioPipelineService` — log pipeline start/stop, VAD segments, transcription results, errors
- [ ] `WasapiAudioCaptureService` — log device open/close, audio format, errors
- [ ] `WhisperSpeechRecognizer` — log model download/load, transcription timing, errors
- [ ] `SileroVadService` — log speech segment detection
- [ ] `WasapiMicrophoneEnumerator` — log device add/remove/state change events
- [ ] `JsonSettingsService` — log setting read/write
- [ ] Build, commit

### Phase 3: Add logging to Desktop ViewModels
- [ ] `SettingsViewModel` — log mic selection, device changes, fallback/auto-select
- [ ] `MainWindowViewModel` — log recording toggle
- [ ] Build, run all tests, commit

## Notes
- Use `ILogger<T>` via constructor injection — DI resolves it automatically when `AddLogging()` is registered
- Log categories follow namespace: `Parlotype.Platform.Audio`, `Parlotype.Desktop.ViewModels`, etc.
- Rolling file: `AppData/Local/parlotype/logs/parlotype-{date}.log`
- Console: colored plain text for development
- Services without explicit constructors will need constructors added to accept `ILogger<T>`
