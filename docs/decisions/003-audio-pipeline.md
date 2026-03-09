---
status: accepted
date: 2026-02-17
---

# 003. Audio Pipeline

## Context

Parlotype needs to capture microphone audio, detect speech segments (to avoid sending silence to the recognizer), and transcribe speech using Whisper. The pipeline must work in real-time on consumer hardware without GPU requirements.

## Decision

Three-stage audio pipeline: WASAPI Capture → Silero VAD → Whisper Transcription.

- **NAudio WasapiCapture** for low-latency microphone input, resampled to 16kHz mono via WdlResamplingSampleProvider (Whisper's required format).
- **Silero VAD** (SileroVad NuGet 1.3.0) for voice activity detection — filters silence before transcription, reducing Whisper workload.
- **Whisper.net** (1.9.0) for on-device speech recognition. Uses the Base model by default (configurable). Greedy decoding for beam size 1.
- **Two pipeline modes**: Batch (accumulate audio, detect end-of-speech via silence, transcribe full segment) and Streaming (fixed 3-second windows).
- **ConcurrentQueue<float[]>** bridges capture and transcription threads. Capture runs on the WASAPI callback thread; transcription runs on a dedicated background thread.
- **BufferedWaveProvider.ReadFully = false** — critical fix to prevent NAudio from padding reads with silence (caused 18x data inflation drowning speech in zeros).

## Consequences

- Easier: VAD dramatically reduces Whisper processing time by only transcribing speech segments. Two modes allow experimentation.
- Easier: Pipeline is observable via TranscriptionAvailable events — UI and text injection can subscribe independently.
- Harder: Audio format conversion (float PCM, sample rate) must be correct at every stage or VAD/Whisper produce garbage. The ReadFully=false lesson was learned the hard way.
- Harder: Batch mode re-scans the entire buffer per VAD call (addressed later by incremental VAD in ADR 008).
