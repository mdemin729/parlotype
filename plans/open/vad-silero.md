# Voice Activity Detection (VAD) with Silero

## Feature Description

Parlotype integrates **Silero Voice Activity Detection (VAD)** to enable intelligent, near-real-time speech transcription. This feature automatically detects when the user is speaking and when they pause, allowing the application to buffer audio efficiently and transcribe complete utterances with minimal latency.

## Overview

Silero VAD is a lightweight, on-device machine learning model that distinguishes between speech and silence in real-time. By combining VAD with Whisper transcription, Parlotype can deliver responsive voice-to-text functionality while maintaining full privacy and offline operation.

## Key Features

- **Real-Time Speech Detection** — Continuously monitors audio stream to identify when speech begins and ends
- **Pause-Based Buffering** — Automatically accumulates audio while speech is detected and triggers transcription when silence is detected
- **Low Latency** — Minimal processing overhead allows near-instantaneous transcription results
- **Lightweight Model** — Silero VAD runs efficiently on CPU without requiring GPU acceleration
- **Privacy-Focused** — All processing happens locally; no audio data leaves the user's machine
- **Seamless Integration** — Works alongside Whisper transcription for a complete voice-to-text pipeline

## How It Works

1. **Audio Streaming** — Audio input is captured continuously (either via hotkey activation or continuous listening mode)
2. **VAD Analysis** — Audio chunks are fed to the Silero VAD model, which returns a probability score indicating the likelihood of speech presence
3. **Speech Detection** — When the probability exceeds the configured threshold, the audio is buffered
4. **Silence Detection** — When silence is detected for a configurable duration, the buffered audio is queued for transcription
5. **Transcription** — The accumulated audio segment is sent to the Whisper model for transcription
6. **Text Injection** — The transcribed text is automatically injected into the active application

## Technical Specifications

- **Model Format** — ONNX (Open Neural Network Exchange) for cross-platform compatibility
- **Model Source** — Silero VAD GitHub repository
- **Input** — 16-bit PCM audio at 16 kHz sample rate
- **Output** — Speech probability score (0.0 to 1.0)
- **Processing** — CPU-based inference with no external dependencies

## Configuration Options

- **Speech Threshold** — Probability threshold for speech detection (default: 0.5)
- **Silence Duration** — Duration of silence required to trigger transcription (e.g., 500ms)
- **Audio Chunk Size** — Size of audio frames processed per VAD inference (e.g., 512 samples)

## Benefits

- **Efficient Resource Usage** — VAD prevents unnecessary transcription attempts on silence
- **Better Accuracy** — Whisper receives complete utterances rather than fragmented audio
- **User-Friendly** — Automatic pause detection provides a natural speaking experience
- **Customizable** — Users can adjust sensitivity and silence duration to match their preferences

## Implementation Details

The feature uses the ONNX Runtime to load and execute the Silero VAD model within the application's audio processing pipeline. Audio frames are processed in real-time, with results fed into the buffering logic that manages the flow of audio to the Whisper transcription engine.
