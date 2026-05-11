---
title: "Session: 2026-05-09 — Gemma 4 llama.cpp Desktop Integration"
type: session
status: active
tags: [gemma4, llamacpp, sidecar, desktop, settings]
created: 2026-05-09
summary: "Integrated Gemma 4 E4B ASR into Desktop app via llama-server sidecar; added Speech Engine + llama.cpp settings pages"
---

# Session: 2026-05-09 — Gemma 4 llama.cpp Desktop Integration

## Active Focus
- **Core:** `SpeechEngine.cs` enum, `Gemma4ModelInfo.cs` record, new `SettingsKeys` (SpeechEngine, LlamaCppServerFolder, LlamaCppPort)
- **Platform:** `LlamaCppSpeechRecognizer.cs` (spawns/adopts llama-server, base64 WAV via `/v1/chat/completions`), `DelegatingSpeechRecognizer.cs` (routes by setting), `SpeechRecognizerFactory.cs`, `Gemma4ModelDownloadService.cs`, `LlamaCppServerInfo.cs` (probes `/health` + `/props`)
- **Desktop:** `SpeechEngineSettingsViewModel` (Whisper/Gemma4 toggle), `LlamaCppSettingsViewModel` (server status, props display, port/folder config, Browse/Save/Reset), corresponding AXAML views
- **DI:** `PlatformServiceExtensions.cs` updated with factory pattern for `ISpeechRecognizer`
- **App lifecycle:** `App.axaml.cs` exit handler disposes speech recognizer to stop llama-server
- **Tests:** 12 new unit tests + 8 screenshot tests across 4 new test files
- **Docs:** ADR-025, vault updates (platform.md, desktop.md, decisions/_index.md)

## Decisions Made
- **ADR-025:** Gemma 4 via llama.cpp sidecar in Desktop — `SpeechEngine` enum, `DelegatingSpeechRecognizer` routing, llama-server process management, Vulkan-only, English-only, E4B model
- **No new project:** llama.cpp integration lives in `Parlotype.Platform` (unlike benchmark-only `Parlotype.Gemma4` Python sidecar)
- **Port conflict UX:** Show error + warning in settings instead of silently picking random port
- **Server adoption:** If a healthy llama-server is already running on the configured port (verified via `/props`), adopt it instead of spawning a new one
- **Folder-based config:** Settings stores `LlamaCppServerFolder` (folder path), appends `llama-server.exe` automatically. Default: `%LOCALAPPDATA%\parlotype\llama-server`
- **Thread safety:** `SemaphoreSlim` in both `LlamaCppSpeechRecognizer` and `DelegatingSpeechRecognizer` for init/unload mutual exclusion
- **Settings save triggers unload:** Changing port or folder calls `recognizer.UnloadAsync()` so next recording re-initializes with new settings

## Facts Learned
- llama-server `/props` endpoint is llama-server-specific — Python sidecar and other servers don't have it; useful for distinguishing llama-server from other processes on the same port
- llama-server `/v1/chat/completions` with `input_audio` blocks works with Gemma 4 E4B on Vulkan (RTX 5070 Ti); transcription quality is good on clean speech (~5.7s prompt processing for a short clip)
- mmproj filename for Gemma 4 E4B is `mmproj-gemma-4-E4B-it-bf16.gguf` (case-sensitive, `bf16` not `f16`); GGUF is `gemma-4-E4B-it-Q4_K_M.gguf` (4.97 GB); mmproj is 0.92 GB
- HuggingFace repo is `ggml-org/gemma-4-E4B-it-GGUF`; files downloadable via `hf download`
- Avalonia 12 doesn't have `BoolConverters.ToYesNo`; use conditional `IsVisible` instead
- `Process.Kill(entireProcessTree: true)` is needed to clean up llama-server on Windows
- llama-server cold start with Gemma 4 E4B Q4 on NVMe + Vulkan takes ~10-15 seconds

## Open Blockers
- None

## Documentation Status
- ADR: done — `docs/decisions/025-gemma4-llamacpp-desktop.md`
- Vault (services/architecture): done — `memory/services/platform.md`, `memory/services/desktop.md`, `memory/decisions/_index.md`
- Knowledge (non-derivable facts): pending — llama-server `/props` endpoint and mmproj naming facts should be distilled

## Next Action
- Distill key non-derivable facts to `memory/knowledge/` (llama-server props endpoint, mmproj naming)
- Consider adding Gemma 4 model download UI (progress dialog) for first-time setup
- Consider conditional visibility of Whisper-specific settings (model picker, translation) when Gemma 4 is selected
- Test end-to-end with fresh install (no pre-existing llama-server or models)
- Monitor llama.cpp releases for audio quality improvements
