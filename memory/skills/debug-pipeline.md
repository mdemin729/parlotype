---
title: Debug Audio Pipeline Skill
type: skill
status: active
tags: [skill, debug, audio, pipeline]
last_updated: 2026-03-28
summary: Step-by-step debugging guide for the audio capture → transcription pipeline
---

# Debug Audio Pipeline

## Before You Start

1. Read [[audio-pipeline]] for the full data flow
2. Read [[platform]] for implementation details
3. Check [[decisions/_index|ADR-003]] and [[decisions/_index|ADR-008]] for design rationale

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
