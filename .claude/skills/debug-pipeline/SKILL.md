---
name: debug-pipeline
description: Use when debugging audio capture, VAD, Whisper transcription, text injection, or threading issues in the Parlotype audio pipeline. Provides a layered checklist from microphone input through final keystroke output.
---

# Debug Audio Pipeline

## Before You Start

1. Read `memory/architecture/audio-pipeline.md` for the full data flow
2. Read `memory/services/platform.md` for implementation details
3. Check `memory/decisions/_index.md` for ADR-003 and ADR-008 design rationale

## Debugging Steps

### 1. Audio Capture Issues
- Check `IAudioCaptureService` / `WasapiAudioCaptureService`
- Verify microphone is selected and accessible
- Check sample rate conversion to 16kHz mono float
- Look for NAudio exceptions in `%LOCALAPPDATA%/parlotype/logs/`

### 2. VAD Issues
- Check `IVoiceActivityDetector` / `SileroVadService`
- Verify ONNX runtime is loading correctly
- Check VAD threshold settings
- Incremental VAD processes chunks — verify chunk boundaries

### 3. Transcription Issues
- Check `ISpeechRecognizer` / `WhisperSpeechRecognizer`
- Verify model is downloaded and loadable
- Check `WhisperOptions` (beam size, language, temperature)
- CUDA issues: try `-p:EnableCuda=false` to isolate
- **Never** load model multiple times in a single run

### 4. Text Injection Issues
- Check `ITextInjectionService` implementations
- Verify `Win32TargetWindowTracker` identifies correct window
- Clipboard-based: check save/restore cycle
- SharpHook-based: check key simulation timing

### 5. Threading Issues
- Capture and transcription on separate threads
- `ConcurrentQueue<float[]>` bridges them
- UI updates must dispatch to `Dispatcher.UIThread`

## Common Patterns

| Symptom | Likely Cause | Check |
|---------|-------------|-------|
| No audio captured | Wrong microphone or permission | Device enumeration, OS permissions |
| VAD never triggers | Threshold too high or ONNX error | VAD settings, ONNX runtime logs |
| Transcription garbled | Wrong sample rate or model | Audio format, model file integrity |
| Text appears in wrong window | Window tracker lost focus | `Win32TargetWindowTracker` logic |
| UI freezes | Background thread updating UI directly | Missing `Dispatcher.UIThread` dispatch |

## See Also
Canonical Obsidian version: `memory/skills/debug-pipeline.md`
