---
title: Glossary
type: index
status: active
last_updated: 2026-03-28
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
| **Avalonia** | Cross-platform .NET UI framework (used instead of WPF/MAUI) |
| **AXAML** | Avalonia XAML — Avalonia's markup format (`.axaml` extension) |
| **SharpHook** | .NET wrapper for libuiohook — provides global keyboard/mouse hooks |
| **NAudio** | .NET audio library used for WASAPI capture and format conversion |
| **Push-to-Talk** | Hotkey mode: hold key to record, release to stop |
| **Toggle** | Hotkey mode: press to start recording, press again to stop |
| **Batch mode** | Transcription mode: buffer audio until end-of-speech, then transcribe |
| **Streaming mode** | Transcription mode: process fixed 3-second windows continuously |
| **Text injection** | Pasting transcribed text into the target application via clipboard or key simulation |
| **Beam search** | Whisper decoding strategy using multiple candidate sequences (beam size > 1) |
| **Greedy decoding** | Whisper decoding strategy selecting best token at each step (beam size = 1) |
