# ADR-025: Gemma 4 via llama.cpp Sidecar in Desktop

- **Status:** Accepted
- **Date:** 2026-05-09
- **Deciders:** Maksim

## Context

Parlotype's speech recognition was previously limited to Whisper.net. Research (see `docs/research/2026-05-09-Gemma-4-Claude-research.md`) showed that Google's Gemma 4 E4B model can perform speech-to-text via its conformer audio encoder, achieving ~4.17% WER on clean speech (LibriSpeech-clean). The only Windows-native runtime that supports Gemma 4 audio over a stable HTTP API is `llama-server` from llama.cpp.

A benchmark-only Python sidecar already existed (`Parlotype.Gemma4`, ADR-024). This ADR covers integrating Gemma 4 into the Desktop application for end-user transcription.

## Decision

Add Gemma 4 E4B as an alternative speech engine in the Desktop app, powered by a managed `llama-server.exe` (llama.cpp, Vulkan build) sidecar process.

### Key design choices

1. **`SpeechEngine` enum in Core** — `Whisper` (default) | `Gemma4`, persisted via `SettingsKeys.SpeechEngine`
2. **`LlamaCppSpeechRecognizer`** implements `ISpeechRecognizer` — spawns `llama-server.exe`, health-polls `/health`, sends audio via `/v1/chat/completions` with `input_audio` base64-encoded WAV blocks
3. **`DelegatingSpeechRecognizer`** — registered as the `ISpeechRecognizer` singleton; resolves to Whisper or LlamaCpp at `InitializeAsync` time based on settings
4. **Model files** — GGUF (~9.6 GB) + mmproj (~150–300 MB) downloaded from HuggingFace `ggml-org/gemma-4-E4B-it-GGUF` to `%LOCALAPPDATA%\parlotype\models\`
5. **Vulkan-only** initially — uses the pre-built `llama-b9090-bin-win-vulkan-x64` binary
6. **English-only** — uses Google's prescribed prompt template for accurate verbatim transcription
7. **No streaming** — `stream: false` for simpler error handling

### Architecture

```
User selects "Gemma 4" in Settings → SpeechEngine
        ↓
DelegatingSpeechRecognizer reads SpeechEngine setting
        ↓
LlamaCppSpeechRecognizer.InitializeAsync()
        ↓
Spawns llama-server.exe with GGUF + mmproj
        ↓
AudioPipeline → float[] → WAV → base64 → /v1/chat/completions
        ↓
Gemma 4 E4B transcribes → text returned → injected into target app
```

## Consequences

### Positive
- Users can choose between Whisper and Gemma 4 for transcription
- Gemma 4 achieves competitive WER on clean speech without a separate Whisper model
- Same `ISpeechRecognizer` interface — `AudioPipelineService` unchanged
- Foundation for future post-processing (grammar correction, summarization) using the same loaded model

### Negative
- Large model download (~10 GB for GGUF + mmproj)
- Gemma 4 E4B has high WER on noisy audio (~41% on AMI vs ~16% for Whisper-large-v3)
- Requires `llama-server.exe` binary on disk (not bundled, path must be configured)
- Additional process to manage (port conflicts, startup time, crash handling)
- English-only (translation support deferred)

### Risks
- Gemma 4's audio conformer is recent (April 2026) — may have edge-case bugs in llama.cpp
- 30-second audio clip limit (already respected by existing VAD pipeline)
- llama-server cold start can take 3–30+ seconds depending on storage speed

## Alternatives Considered

1. **Python sidecar (ADR-024)** — already exists for benchmarks, but requires Python runtime and has CUDA/Blackwell issues
2. **LLamaSharp / P/Invoke** — more control, no separate process, but significantly more engineering and loses runtime-choice flexibility
3. **Ollama** — smoother UX but Gemma audio is not yet supported (issue #15333)
4. **Lemonade (AMD)** — strong on Ryzen AI hardware but not cross-vendor for GPU acceleration

## Related

- ADR-024: Gemma 4 Python Sidecar (benchmark-only)
- `docs/research/2026-05-09-Gemma-4-Claude-research.md`
