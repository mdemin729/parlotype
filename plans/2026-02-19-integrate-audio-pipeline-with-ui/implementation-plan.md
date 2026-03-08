---
title: Integrate audio pipeline with UI
status: completed
created: 2026-02-19
started: 2026-02-19
completed: 2026-02-19
---

# Wire Recording to Audio Pipeline

## Problem
`ToggleRecording` in `MainWindowViewModel` only toggles a boolean — it doesn't start/stop the audio pipeline. Need to connect it so recording actually captures audio, runs it through VAD + Whisper, and logs transcription results at Debug level.

## Approach
Inject `IAudioPipeline` into `MainWindowViewModel`. On record start, call `pipeline.StartAsync()`. On record stop, call `pipeline.StopAsync()`. Subscribe to `TranscriptionAvailable` and log the result text at Debug level. Make `ToggleRecording` async to await pipeline start/stop.

## Workplan

- [ ] Inject `IAudioPipeline` into `MainWindowViewModel` constructor
- [ ] Make `ToggleRecording` async — start/stop pipeline, subscribe to `TranscriptionAvailable`
- [ ] Log transcription results at Debug level
- [ ] Handle errors gracefully (log + reset state)
- [ ] Update headless tests (pass `null` or mock for `IAudioPipeline`)
- [ ] Build (0 warnings), run all tests, commit
