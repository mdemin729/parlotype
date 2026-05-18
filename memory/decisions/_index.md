---
title: Architecture Decisions
type: index
status: active
last_updated: 2026-05-17
summary: Index of all Architecture Decision Records (ADRs) for Parlotype
---

# Architecture Decisions

ADR source files live in `docs/decisions/`. This index provides a quick reference.

| # | Decision | Status | Key Impact |
|---|----------|--------|------------|
| 001 | [[001-global-hotkey-sharphook\|Global Hotkey via SharpHook]] | Accepted | SharpHook for cross-platform global keyboard hooks |
| 002 | [[002-solution-architecture\|Solution Architecture]] | Accepted | Core → Platform → Desktop layered architecture |
| 003 | [[003-audio-pipeline\|Audio Pipeline]] | Accepted | WASAPI → 16kHz mono → VAD → Whisper pipeline |
| 004 | [[004-json-settings-persistence\|JSON Settings Persistence]] | Accepted | `JsonSettingsService` with `SemaphoreSlim` thread safety |
| 005 | [[005-zlogger-structured-logging\|ZLogger Structured Logging]] | Accepted | ZLogger for console + rolling file logging |
| 006 | [[006-text-injection\|Text Injection: Clipboard vs SharpHook]] | Accepted | Clipboard-based injection as default |
| 007 | [[007-whisper-model-selection\|Whisper Model Selection & Download]] | Accepted | Enum-based model selection with HTTP download + progress |
| 008 | [[008-incremental-vad\|Incremental VAD Processing]] | Accepted | Incremental Silero VAD for lower latency |
| 009 | [[009-benchmark-cli-design\|Benchmark CLI Design]] | Accepted | System.CommandLine + Spectre.Console + SQLite |
| 010 | [[010-avalonia-headless-testing\|Avalonia Headless UI Testing]] | Accepted | Avalonia.Headless.XUnit for UI tests |
| 011 | [[011-optimal-stt-pipeline\|Optimal STT Pipeline Settings]] | Accepted | Benchmark-driven optimal Whisper parameters |
| 012 | [[012-cuda-gpu-acceleration\|CUDA GPU Acceleration]] | Accepted | Optional NVIDIA CUDA via Whisper.net.Runtime.Cuda |
| 013 | [[013-obsidian-memory-vault\|Obsidian Memory Vault for AI Agents]] | Accepted | Persistent memory substrate for AI agents in `memory/` |
| 014 | [[014-nvidia-environment-detection\|NVIDIA/CUDA Environment Detection]] | Accepted | First-party provider logs driver/toolkit/runtime at startup |
| 015 | [[015-parlotype-desktop-v2-avalonia12\|Parlotype.Desktop — Avalonia 12 Tray-Based UI]] | Accepted | Tray-first frontend on Avalonia 12 (originally Desktop.V2, renamed after V1 sunset) |
| 016 | [[016-avalonia12-developer-tools\|Avalonia 12 Developer Tools (V2)]] | Accepted | DEBUG-only `AvaloniaUI.DiagnosticsSupport` + `avdt` global tool replace retired classic `Avalonia.Diagnostics` in V2 |
| 017 | [[017-whisper-model-hot-swap\|Whisper Model Hot-Swap via UnloadAsync]] | Accepted | `ISpeechRecognizer.UnloadAsync` enables model switching without app restart |
| 018 | [[018-v1-sunset-consolidation\|Sunset V1 — Consolidate on V2]] | Accepted | Removed `Parlotype.Desktop` (V1); implemented WaitTime, punctuation, profanity features in V2 end-to-end |
| 019 | [[019-remove-sub-500ms-silence-threshold\|Remove Sub-500ms Silence Threshold]] | Accepted | Removed Instant/VeryShort/Short from WaitTimeOption; benchmark proved sub-500ms causes 77%+ WER |
| 020 | [[020-sharphook7-simple-global-hook\|Upgrade SharpHook 7 + SimpleGlobalHook]] | Accepted | Switched to SimpleGlobalHook for working event suppression; upgraded SharpHook 6→7.1.1 |
| 021 | [[021-whisper-translation-to-english\|Whisper Translation to English via Settings]] | Accepted | Settings-driven translation toggle; recognizer reinitializes when options change |
| 022 | [[022-vulkan-gpu-acceleration\|Vulkan GPU Acceleration]] | Accepted | `Whisper.net.Runtime.Vulkan` added unconditionally; `RuntimePreference` extended to Auto/Cuda/Vulkan/Cpu with strict no-fallback semantics; new Settings → Runtime section |
| 023 | [[023-audio-level-waveform-visualisation\|Audio-Level Provider & Waveform Visualisation]] | Accepted | `IAudioLevelProvider` interface; `WaveformView` custom control with three visual states (mic icon / breathing bars / animated wave); EMA-smoothed RMS state machine |
| 024 | [[024-gemma4-python-sidecar\|Gemma 4 Python Sidecar]] | Accepted | `Parlotype.Gemma4` project with `Gemma4SpeechRecognizer` implementing `ISpeechRecognizer`; auto-managed Python FastAPI sidecar for benchmark-only Gemma 4 E2B/E4B ASR; bitsandbytes quantization (4-bit default) |
| 025 | [[025-gemma4-llamacpp-desktop\|Gemma 4 via llama.cpp in Desktop]] | Accepted | `SpeechEngine` enum (Whisper/Gemma4); `LlamaCppSpeechRecognizer` spawns llama-server with Vulkan; `DelegatingSpeechRecognizer` routes by setting; English-only, E4B model |
| 026 | [[026-managed-llama-server-install\|Managed llama.cpp Server Installation]] | Accepted | `ILlamaServerCatalog`/`ILlamaServerInstaller`/`ILlamaServerRegistry` in Core; `GitHubLlamaServerCatalog` (HTTP+ETag+1h cache), `JsonLlamaServerRegistry` (manifest.json + `LlamaCppActiveInstall` selector), `LlamaServerInstaller` (staging+SHA256+atomic-rename, cudart companion merge) in Platform; reworked llama settings UI with managed Installed/Available lists and a distinct Manual panel; Phase-1 Windows-only |

## Template

New ADRs use `docs/decisions/_template.md`. Use the next sequential number — list `docs/decisions/` and pick `(highest existing number) + 1`. Do **not** rely on a hard-coded "next" hint here, it goes stale.
