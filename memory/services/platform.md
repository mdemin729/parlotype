---
title: Parlotype.Platform
type: service-profile
status: active
tags: [platform, implementations, whisper, nAudio, vad]
criticality: high
last_updated: 2026-07-13
summary: Implements Core interfaces using Whisper.net, sherpa-onnx, NAudio, SileroVad, SharpHook
---

# Parlotype.Platform

## Purpose
Platform-specific implementations of all Core interfaces. Where the real audio capture, VAD, transcription, hotkeys, and settings logic lives.

## Key Paths
- `src/Parlotype.Platform/Audio/` — `WasapiAudioCaptureService`, `SileroVadService`, `MicrophoneEnumerator`, `AudioPipelineService` (also implements `IAudioLevelProvider`; `CacheSettingsAsync` reads `SelectedSourceLanguage` into `WhisperOptions.Language` — ADR-034 — resolving the `keyboard` sentinel via `IKeyboardLayoutService` + `SourceLanguageResolver`, validated against the Whisper language set — ADR-036; `PrewarmAsync` + `StartAsync` share `EnsureModelInitializedAsync` under `SemaphoreSlim _initLock` — ADR-038)
- `src/Parlotype.Platform/Speech/` — `WhisperSpeechRecognizer`, `LlamaCppSpeechRecognizer` (Gemma 4 via llama-server sidecar; speech consumer of `LlamaServer`; `BuildPromptTextAsync` resolves the active `PromptTemplate` per `TranscribeAsync` call and selects a body via a source/target matrix (ADR-037): the built-in default carries dedicated transcription / `TranslationText` / `AutoDetectText` bodies (`{speech_lang}`/`{text_lang}` tokens — retiring `{language}`; `{speech_lang}`→"the detected language" when auto), while a custom single-body prompt has the translation sentence appended. Translation is gated on the `TranslationEnabled` toggle (kept for Gemma 4, parity with Whisper) **and** a real target ≠ resolved source (keyboard sentinel resolved via `IKeyboardLayoutService`, ADR-036) — single LLM call, ADR-034; no model reload on prompt change; also implements `ILlamaCppServerLifecycle` so the installer can stop it before deleting files), `JsonPromptTemplateRegistry` (`prompts.json` + `ActivePromptId` selector; non-deletable built-in default merged at read time, corrupt-file quarantine; ADR-030), `ParakeetSpeechRecognizer` (NVIDIA Parakeet TDT 0.6B v3 via sherpa-onnx `OfflineRecognizer`, in-process, CPU-only INT8, `nemo_transducer` model type, greedy search; model load + decode wrapped in `Task.Run`; always auto-detects among 25 European languages — no language-forcing parameter; transcribe-only; **default engine** — `SpeechRecognizerFactory` falls back to it when `SpeechEngine` is unset, and on first use it ensures the ~670 MB model via an optional `IParakeetModelProvider` ctor dependency — headless `ParakeetModelDownloadService` in Platform, overridden by the Desktop download dialog with progress + Cancel (last-wins DI); decline ⇒ `OperationCanceledException`, ADR-041/042), `ParakeetModelDownloadService` (4-file HF download — encoder/decoder/joiner/tokens ~670 MB — into per-model subdir `models/<modelId>/`, cumulative progress + delete; ADR-041), `DelegatingSpeechRecognizer` (routes by `SpeechEngine` setting), `SpeechRecognizerFactory`, `Gemma4ModelDownloadService` (per-model download/delete of the 5-entry GGUF+mmproj catalog with cumulative progress; streams scoped before atomic move, ADR-029), `HttpModelDownloadService` (delegates the download loop to `StreamingFileDownloader`), `StreamingFileDownloader` (shared HTTP → temp → atomic-move helper), `WhisperModelTypeExtensions`, `WhisperRuntimeBootstrap` (CUDA/Vulkan/CPU runtime selection + Whisper.net diagnostics bridge), `WindowsNvidiaEnvironmentProvider`, `NoOpNvidiaEnvironmentProvider`, `WindowsVulkanEnvironmentProvider`, `NoOpVulkanEnvironmentProvider`, `Win32KeyboardLayoutService` (P/Invoke `GetKeyboardLayout` on the **focused input control's thread** of the foreground window — `ResolveInputThread` uses `GetGUIThreadInfo(foregroundThread).hwndFocus` to cross from a packaged app's stale frame thread into its real text-input thread, e.g. Windows 11 Notepad; HKL low-word LANGID → `CultureInfo` → code + English name; transient LANGIDs degrade to null — ADR-036) + `NoOpKeyboardLayoutService` (non-Windows), `OpenAiCompatibleSpeechRecognizer` (cloud, opt-in BYOK — multipart `POST {OpenAiCompatBaseUrl}/audio/transcriptions` with `file`/`model`/`response_format=json`/`temperature=0` + optional `language`; defaults `https://api.openai.com/v1` + `gpt-4o-mini-transcribe`; base-URL swap covers OpenAI/Groq/any OpenAI-protocol host; fresh 60 s-timeout `HttpClient` per init with Bearer key from `ISecretStore`, missing key ⇒ actionable `InvalidOperationException`; internal `MessageHandlerOverride` test seam — ADR-043), `XaiGrokSpeechRecognizer` (cloud — `POST {XaiGrokBaseUrl}/stt`, field `format` not `response_format`, response `text` falling back to `transcript`; defaults `https://api.x.ai/v1` + `grok-stt` — ADR-043), `WavEncoder` (internal 16-bit PCM WAV encoder extracted from `LlamaCppSpeechRecognizer`, shared by LlamaCpp + both cloud recognizers; cloud recognizers send **no `language` part** — always auto-detect, matching their hidden language UI, ADR-043 amendment), `CloudSpeechHttpError` (shared failure mapping — ADR-043 amendment: parses the provider error envelope (OpenAI `{"error":{"message",…,"code"}}` + variants) rather than dumping raw JSON, classifies into `CloudSpeechErrorKind` from status + code (401/403 ⇒ KeyRejected, 429 `insufficient_quota`/quota text ⇒ QuotaExceeded else RateLimited, 5xx ⇒ ProviderUnavailable), throws typed `CloudSpeechTranscriptionException`; logs full body but never Authorization). Cloud recognizers do **not** call `TranscriptionTextProcessor` — `AudioPipelineService` post-processes all recognizer output centrally (ADR-043)
- `src/Parlotype.Platform/LlamaServer/` — managed-install subsystem (ADR-026, namespace flattened in ADR-027 — workload-agnostic, shared by speech today and future post-processing): `JsonLlamaServerRegistry` (manifest.json + `LlamaCppActiveInstall` selector), `LlamaServerAssetParser` (tolerant filename parser), `LlamaServerAssetDescriptor` (parser-internal projection), `GitHubLlamaServerCatalog` (HTTP + ETag + 1h on-disk cache, OS/arch filter at read time), `LlamaServerInstaller` (staging dir + SHA256 verify + ZIP extract + atomic Directory.Move + cudart companion merge; `BuildInstallId` is public so the UI can pair catalog rows with installed entries), `LlamaCppServerInfo` (probe helper: `/health` + `/props` for adopt-vs-spawn decision, relocated here in ADR-027)
- `src/Parlotype.Platform/Hotkeys/` — `SharpHookHotkeyService` (thin adapter over `SimpleGlobalHook`: builds `HotkeyKeyEvent`, delegates to Core's `HotkeyGestureMatcher`, raises semantic dictation events; owns a `Timer` for deferred hold-starts and a lock around the matcher since the hook thread and timer thread both reach it — ADR-047), `KeyCodeMapper` (key names ↔ `KeyCode`, modifier codes → `ModifierKey` + `ModifierSide`, `EventMask` → `HotkeyModifiers`, `IsRightAltHeld` for the AltGr filter)
- `src/Parlotype.Platform/Settings/` — `JsonFileStore` (abstract, shared JSON key-value persistence: file path + own `SemaphoreSlim` lock + load/save), `JsonSettingsService` (`ISettingsService` → `settings.json`), `JsonWindowStateService` (`IWindowStateService` → `window-state.json`, kept in a separate file from `settings.json` on purpose so window-drag saves never touch or contend with user settings — ADR-040), `DpapiSecretStore` (`ISecretStore` → `secrets.json`, deliberately outside `settings.json`; standalone impl — not on `JsonFileStore`, which lacks per-key delete and a protect hook; Windows: DPAPI `ProtectedData` CurrentUser scope; non-Windows: base64 plaintext + one-time warning; undecryptable values treated as absent, never fatal — ADR-043)
- `src/Parlotype.Platform/PlatformServiceExtensions.cs` — DI registration (all singletons)
- 2026-07 perf/security rework (ADR-045/046): `AudioPipelineService` is now three channel-joined single-threaded stages — capture callback (RMS + pooled copy only) → segmenter task (owns the sample buffer, VAD/segmentation, unchanged thresholds) → transcription task (`ReadAllAsync`, no polling); stop = channel completion with drain + 30 s cap; `WasapiAudioCaptureService` rents callback buffers from `ArrayPool<float>` (see [[wasapi-capture-buffer-sizing]]); `WavEncoder` writes one exact-size array via `BinaryPrimitives` (byte-identical to the old MemoryStream encoder); `StreamingFileDownloader` + Parakeet/Gemma download loops verify SHA-256 while streaming (mismatch ⇒ Core `ModelIntegrityException`, fail-closed; missing digest ⇒ warn, fail-open); cloud recognizers enforce `CloudBaseUrlValidator` (HTTPS-or-loopback) at init; `ClipboardTextInjectionService` sets the Windows clipboard exclusion formats on injected text (see [[windows-clipboard-exclusion-formats]]); llama-server spawned via `ArgumentList`; `AtomicFileWriter` (temp + move) backs `JsonFileStore` + `DpapiSecretStore` saves; transcripts are never logged (S1 convention — log lengths only)

## Key External Dependencies
- **Whisper.net** + **Whisper.net.Runtime.Cuda** (conditional, ~350 MB) + **Whisper.net.Runtime.Vulkan** (always, ~30 MB) — transcription
- **NAudio** — WASAPI audio capture
- **Microsoft.ML.OnnxRuntime** — Silero VAD ONNX inference
- **org.k2fsa.sherpa.onnx** — Parakeet TDT v3 inference (bundles its own onnxruntime native libs; CPU-only prebuilt — see [[sherpa-onnx-quirks]])
- **SharpHook** — global keyboard hooks
- **System.Security.Cryptography.ProtectedData** — DPAPI encryption for `DpapiSecretStore` (Windows-only code path; package is cross-platform-safe — ADR-043)

## Conventions
- Register all new services in `PlatformServiceExtensions.cs`
- Mirror Core's subfolder structure
- Runtime selection: `Auto` chains CUDA → Vulkan → CPU silently; `Cuda` and `Vulkan` are strict and throw `RuntimeUnavailableException` when unavailable (no silent CPU fallback)
- NVIDIA + Vulkan env provider and keyboard-layout service DI selection is gated by `OperatingSystem.IsWindows()` (Windows impl vs no-op)
- `WhisperRuntimeBootstrap` bridges Whisper.net's internal log via `LogProvider.AddLogger`; remaps the inverted `WhisperLogLevel` enum (see [[whisper-net-quirks]])

## Dependencies
- [[core]]

## Related Decisions
- [[decisions/_index|ADR-003]] Audio pipeline
- [[decisions/_index|ADR-008]] Incremental VAD
- [[decisions/_index|ADR-012]] CUDA GPU acceleration
- [[decisions/_index|ADR-014]] NVIDIA/CUDA environment detection
- [[decisions/_index|ADR-017]] Whisper model hot-swap via `UnloadAsync`
- [[decisions/_index|ADR-022]] Vulkan GPU acceleration
- [[decisions/_index|ADR-023]] Audio-level provider (RMS computation in `AudioPipelineService`)
- [[decisions/_index|ADR-038]] Speech-model prewarm (`AudioPipelineService.PrewarmAsync`, `_initLock`)
- [[decisions/_index|ADR-025]] Gemma 4 via llama.cpp sidecar
- [[decisions/_index|ADR-026]] Managed llama.cpp server installation (catalog + registry + installer; manual folder mode preserved)
- [[decisions/_index|ADR-027]] LlamaServer namespace rescope (moved out of `Speech.*` to flat `Parlotype.*.LlamaServer.*` anticipating post-processing consumer)
- [[decisions/_index|ADR-030]] Configurable Gemma 4 prompts (`JsonPromptTemplateRegistry`; recognizer reads active prompt per call)
- [[decisions/_index|ADR-036]] Language UX rebuild (`Win32KeyboardLayoutService` P/Invoke; pipeline + Gemma prompt resolve the keyboard sentinel)
- [[decisions/_index|ADR-041]] Parakeet TDT v3 via sherpa-onnx (`ParakeetSpeechRecognizer`, `ParakeetModelDownloadService`)
- [[decisions/_index|ADR-043]] Cloud speech providers v1 (`OpenAiCompatibleSpeechRecognizer`, `XaiGrokSpeechRecognizer`, `WavEncoder`, `DpapiSecretStore`, cloud helpers)
- [[decisions/_index|ADR-044]] Micro-benchmark project (frozen legacy copies live in `Parlotype.MicroBenchmarks`, `InternalsVisibleTo` for `WavEncoder`)
- [[decisions/_index|ADR-045]] Audio pipeline allocation & threading rework (pooled capture buffers, channel stages, WavEncoder rewrite, Parakeet zero-copy)
- [[decisions/_index|ADR-046]] Security hardening batch (SHA-256 download verification, URL validation, clipboard exclusion, atomic writes, log hygiene)
