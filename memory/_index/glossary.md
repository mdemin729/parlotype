---
title: Glossary
type: index
status: active
last_updated: 2026-05-21
summary: Domain terminology and abbreviations used in Parlotype
---

# Glossary

| Term | Definition |
|------|-----------|
| **VAD** | Voice Activity Detection — Silero-based algorithm that detects speech vs silence in audio |
| **WER** | Word Error Rate — primary metric for transcription accuracy (lower is better) |
| **CER** | Character Error Rate — character-level transcription accuracy metric |
| **RTF** | Real-Time Factor — processing time / audio duration (< 1.0 means faster than real-time) |
| **WASAPI** | Windows Audio Session API — low-level Windows audio capture interface |
| **Whisper** | OpenAI's speech recognition model, used via Whisper.net (.NET binding) |
| **GGML** | Georgi Gerganov's ML tensor library — format for Whisper model weights |
| **GGUF** | Successor format to GGML used by llama.cpp (Gemma 4 weights ship as GGUF) |
| **llama.cpp** | C/C++ inference engine for GGUF models; Parlotype uses `llama-server` as a sidecar for Gemma 4 (ADR-025/026) |
| **llama-server** | HTTP API binary from llama.cpp; spawned/adopted by `LlamaCppSpeechRecognizer` |
| **Gemma 4** | Google multimodal (audio+text) model; alternative ASR engine to Whisper via llama.cpp sidecar (ADR-025) |
| **SpeechEngine** | Enum selecting active recognizer (Whisper or Gemma4); routed by `DelegatingSpeechRecognizer` (ADR-025) |
| **LlamaServer** | Workload-agnostic catalog/registry/installer subsystem for managed llama-server installs (ADR-026/027) |
| **Avalonia** | Cross-platform .NET UI framework (used instead of WPF/MAUI); Parlotype is on Avalonia 12 |
| **AXAML** | Avalonia XAML — Avalonia's markup format (`.axaml` extension) |
| **SharpHook** | .NET wrapper for libuiohook — provides global keyboard/mouse hooks |
| **NAudio** | .NET audio library used for WASAPI capture and format conversion |
| **Push-to-Talk** | Hotkey mode: hold key to record, release to stop |
| **Toggle** | Hotkey mode: press to start recording, press again to stop |
| **Batch mode** | Transcription mode: buffer audio until end-of-speech, then transcribe |
| **Streaming mode** | Transcription mode: process fixed 3-second windows continuously |
| **WaitTime** | Silence-threshold setting that triggers end-of-speech in batch mode; minimum is `Medium` (500 ms) since ADR-019 |
| **Text injection** | Pasting transcribed text into the target application via clipboard or key simulation |
| **Beam search** | Whisper decoding strategy using multiple candidate sequences (beam size > 1) |
| **Greedy decoding** | Whisper decoding strategy selecting best token at each step (beam size = 1) |
| **Runtime preference** | `Auto` / `Cuda` / `Vulkan` / `Cpu` selector for Whisper.net backend; `Cuda`/`Vulkan` are strict (no silent CPU fallback) — see ADR-022 |
| **ADR** | Architecture Decision Record (under `docs/decisions/`); see `memory/decisions/_index.md` |

