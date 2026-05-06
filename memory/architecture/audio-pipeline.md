---
title: Audio Pipeline Architecture
type: architecture
status: active
tags: [audio, vad, whisper, pipeline]
services: [core, platform, desktop]
last_updated: 2026-03-28
summary: End-to-end audio capture, VAD, transcription, and text injection pipeline
---

# Audio Pipeline

## Data Flow

```
WASAPI Capture → 16kHz Mono Float → Silero VAD → Speech Segments → Whisper Transcription → Text Injection
```

## Pipeline Stages

### 1. Audio Capture (NAudio / WASAPI)
- Captures from selected microphone device via `IAudioCaptureService`
- Platform impl: `WasapiAudioCaptureService` in [[platform]]
- Outputs raw PCM float samples at device sample rate

### 2. Format Conversion
- Resamples to **16kHz mono float** (Whisper's required input format)
- Handled within the capture pipeline

### 3. Voice Activity Detection (Silero VAD)
- `IVoiceActivityDetector` (Core) → `SileroVadService` (Platform)
- Detects speech start/end boundaries
- Incremental processing — see [[decisions/_index|ADR-008]]

### 4. Transcription (Whisper.net)
- `ISpeechRecognizer` (Core) → `WhisperSpeechRecognizer` (Platform)
- Configured via `WhisperOptions` record (model, language, beam size, temperature, threads)
- Greedy decoding for beam size 1, beam search for larger values
- CUDA / Vulkan GPU acceleration when available, CPU fallback automatic in `Auto` mode (`Cuda` and `Vulkan` are strict — see [[decisions/_index|ADR-022]])

### 5. Text Injection
- `ITextInjectionService` (Core) → two implementations:
  - `ClipboardTextInjectionService` (default): save clipboard → set text → Ctrl+V → restore
  - `SharpHookTextInjectionService`: direct key simulation
- `Win32TargetWindowTracker` tracks last non-Parlotype foreground window

## Threading Model

- Capture and transcription run on **separate threads**
- `ConcurrentQueue<float[]>` bridges capture → transcription
- UI updates dispatch to `Avalonia.Threading.Dispatcher.UIThread`

## Operating Modes

| Mode | Behavior | Trigger |
|------|----------|---------|
| **Batch** (default) | Buffer audio, detect end-of-speech via silence, then transcribe | Silence threshold |
| **Streaming** | Process fixed 3-second windows continuously | Timer-based |
