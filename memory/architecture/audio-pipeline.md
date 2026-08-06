---
title: Audio Pipeline Architecture
type: architecture
status: active
tags: [audio, vad, whisper, gemma4, llamacpp, pipeline]
services: [core, platform, desktop]
last_updated: 2026-07-13
summary: End-to-end audio capture, VAD, transcription (Whisper or Gemma 4 via llama.cpp), and text injection pipeline
---

# Audio Pipeline

## Data Flow

```
WASAPI Capture → 16kHz Mono Float → Silero VAD → Speech Segments → ISpeechRecognizer → Text Injection
                                                                          │
                                                       ┌──────────────────┴──────────────────┐
                                                       ▼                                     ▼
                                              WhisperSpeechRecognizer            LlamaCppSpeechRecognizer
                                                 (Whisper.net)                   (llama-server sidecar, Gemma 4)
```

`DelegatingSpeechRecognizer` selects the active implementation at runtime based on `SettingsKeys.SpeechEngine` (`Whisper` / `Gemma4`) — see [[decisions/_index|ADR-025]].

> **Extension point — future cloud providers.** `ISpeechRecognizer` is the integration seam for cloud / online speech providers (planned, opt-in). Adding a provider means a new `SpeechEngine` enum value, a new recognizer in Platform, and a new branch in `SpeechRecognizerFactory`. Audio leaves the device **only** when a cloud recognizer is explicitly selected. Brand framing: **local by default, cloud by choice** — see [[../knowledge/brand-positioning]] and [[decisions/_index|ADR-032]].

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
- `AudioPipelineService` also exposes EMA-smoothed RMS via `IAudioLevelProvider` for the waveform UI ([[decisions/_index|ADR-023]])

### 4. Transcription

**Whisper engine** (default):
- `ISpeechRecognizer` (Core) → `WhisperSpeechRecognizer` (Platform)
- Configured via `WhisperOptions` record (model, language, beam size, temperature, threads, translate)
- Greedy decoding for beam size 1, beam search for larger values
- CUDA / Vulkan GPU acceleration: `RuntimePreference.Auto` chains CUDA → Vulkan → CPU; `Cuda` and `Vulkan` are strict and throw `RuntimeUnavailableException` rather than silently falling back ([[decisions/_index|ADR-022]])
- Model hot-swap supported via `ISpeechRecognizer.UnloadAsync()` ([[decisions/_index|ADR-017]]) — never two models loaded simultaneously

**Gemma 4 engine** (alternative, desktop + benchmark):
- `LlamaCppSpeechRecognizer` (Platform) spawns or adopts `llama-server` (Vulkan backend) and POSTs audio to its `/v1/audio/transcriptions` endpoint
- English-only, E4B model by default; managed install via `LlamaServer` subsystem ([[decisions/_index|ADR-026]])
- Benchmark-only Python sidecar variant lives in `Parlotype.Gemma4` ([[decisions/_index|ADR-024]])

### 5. Text Injection
- `ITextInjectionService` (Core) → two implementations:
  - `ClipboardTextInjectionService` (default): save clipboard → set text → Ctrl+V → restore
  - `SharpHookTextInjectionService`: direct key simulation
- `Win32TargetWindowTracker` tracks last non-Parlotype foreground window

## Threading Model (reworked 2026-07, [[decisions/_index|ADR-045]])

Three single-threaded stages joined by unbounded `System.Threading.Channels`:

```
capture callback ──Channel<RawChunk>──▶ segmenter task ──Channel<float[]>──▶ transcription task
(RMS + pooled copy only)               (owns sample buffer, VAD,             (ReadAllAsync, raises
                                        segmentation, final flush)            pipeline events)
```

- The capture callback does **no inference** — a slow VAD on the callback
  thread risked silent audio drops via `DiscardOnBufferOverflow`
  (see [[../knowledge/wasapi-capture-buffer-sizing]])
- Capture buffers are `ArrayPool`-rented; `AudioDataEventArgs.Buffer` is valid
  **only during the event** (subscribers copy synchronously — contract in Core)
- Shutdown = channel completion: StopAsync completes the raw writer, the
  segmenter drains + flushes + completes the utterance writer, the
  transcription loop drains (30 s cap). No polling loops remain.
- `CancelAsync` is the discard counterpart (ADR-057), sharing
  `ShutdownAsync(discard, ct)`: `_discarding` + a cancelled `_transcribeCts` are
  set *before* the writer completes, so the segmenter skips its final flush and
  the transcription loop drains without recognizing. Cancellation is not a
  failure — it does not raise `TranscriptionFailed`. 5 s cap.
- Two concurrency hardenings added after code review of ADR-057: (1)
  `TranscribeLoopAsync` rechecks its own session-scoped `cancellationToken`
  right before publishing — sherpa-onnx can't observe cancellation mid-decode,
  so a call already in flight can return normally well after the drain gave up
  waiting, and without the recheck that result could land in a session that
  had since restarted; (2) `StartAsync`/`ShutdownAsync` serialize behind a
  `_lifecycleLock`, since nothing upstream guarantees a stop/cancel and a start
  never overlap and two overlapping shutdown calls could otherwise corrupt a
  newly-started session's fields.
- Transcripts are never logged (security audit S1, [[decisions/_index|ADR-046]])
- UI updates dispatch to `Avalonia.Threading.Dispatcher.UIThread`

## Operating Modes

| Mode | Behavior | Trigger |
|------|----------|---------|
| **Batch** (default) | Buffer audio, detect end-of-speech via silence, then transcribe | `WaitTime` silence threshold (minimum **500 ms**; sub-500 ms options removed in [[decisions/_index|ADR-019]] because they caused 77%+ WER) |
| **Streaming** | Process fixed 3-second windows continuously | Timer-based |

