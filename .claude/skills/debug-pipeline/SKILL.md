---
name: debug-pipeline
description: Use when debugging audio capture, VAD, speech recognition (Parakeet, Whisper, Gemma 4, cloud), text injection, or threading issues in the Parlotype audio pipeline. Provides a layered checklist from microphone input through final keystroke output.
---

# Debug Audio Pipeline

## Before you start

1. Read `docs/architecture/audio-pipeline.md` (data flow, threading model, stop/cancel,
   modes, utterance ceilings).
2. Skim the ADRs behind it: 003 (pipeline), 008 (incremental VAD), 045 (allocation and
   threading), 057 (cancel on command shortcuts), 060 (hold-scoped push-to-talk).
3. Check `docs/knowledge/README.md` for a note on the dependency involved (NAudio/WASAPI,
   sherpa-onnx, Whisper.net, SharpHook, clipboard).
4. Logs: `%LOCALAPPDATA%/parlotype-data/logs/`. The file sink is capped at `Information`;
   run from a console for `Debug`. Recognized text is never logged, only lengths.

## Work down the layers

### 1. Capture (`IAudioCaptureService` / `WasapiAudioCaptureService`, `WasapiMicrophoneEnumerator`)
- Is the intended device opened? Trace `SettingsKeys.SelectedMicrophoneId` all the way to
  `_capture.StartAsync(device, ct)` (resolved in `AudioPipelineService.CacheSettingsAsync`).
  A setting that is written but never read back looks exactly like a device-matching bug.
- Is the stream converted to 16 kHz mono float?
- `AudioDataEventArgs.Buffer` may be pooled and is valid only during the event. Copy it if
  you keep it.

### 2. VAD (`IVadService` / `SileroVadService`, `VadOptions`, `SpeechSegmentExtractor`)
- Does the Silero ONNX model load? Look for the error at startup.
- Thresholds and padding: see `docs/knowledge/vad-tuning.md` before changing a default.
- VAD runs incrementally on new samples only; check chunk boundaries.

### 3. Recognition (`ISpeechRecognizer` → `DelegatingSpeechRecognizer`)
- First establish **which engine ran**: `SettingsKeys.SpeechEngine` (unset means Parakeet).
  `SpeechRecognizerFactory` resolves it on each `InitializeAsync`.
- Model present and loadable? Parakeet and Whisper models live under the data root's
  `models/`.
- Whisper GPU trouble: set the runtime to `Cpu` (Settings → Speech engine → Whisper runtime)
  and restart; Vulkan is the only GPU backend and selection is one-shot per process.
- Text missing from a long utterance: check the `SpeechEngineLimits` ceiling for the engine
  (Parakeet loses words past 60 s, so long audio is split on speech boundaries).
- Never load two Whisper models at once.

### 4. Injection (`ITextInjectionService`: `ClipboardTextInjectionService` default, `SharpHookTextInjectionService`)
- Did `Win32TargetWindowTracker` record the right foreground window?
- Clipboard path: is the save → paste → restore cycle intact?
- Key-simulation path: timing and keyboard layout.

### 5. Threading
- Three single-threaded stages joined by `System.Threading.Channels`: capture callback (no
  inference) → segmenter (VAD) → transcription. A stall in one stage shows up as a growing
  channel, not as an exception.
- UI state changes must go through `Dispatcher.UIThread`.

## Symptom table

| Symptom | Likely cause | Check |
|---|---|---|
| No audio captured | Wrong device or OS permission | Device enumeration, Windows microphone privacy setting |
| VAD never triggers | Threshold too high, ONNX load error | `VadOptions`, startup log |
| Wrong engine's output | Settings select another engine | `SettingsKeys.SpeechEngine` |
| Garbled transcript | Wrong sample rate or model | Capture format, model file integrity |
| End of a long dictation missing | Utterance ceiling | `SpeechEngineLimits` |
| Text in the wrong window | Tracker lost the target | `Win32TargetWindowTracker` |
| UI freezes | Background thread touching UI | Missing `Dispatcher.UIThread` dispatch |
